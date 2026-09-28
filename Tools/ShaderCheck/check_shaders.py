#!/usr/bin/env python3
"""Syntax-/Typprüfung der eigenen Unity-Shader ohne Unity.

Extrahiert jeden CGPROGRAM-Block (samt vorangestellter CGINCLUDE-Blöcke, wie Unity sie einfügt), ersetzt
UnityCG.cginc/Lighting.cginc/AutoLight.cginc durch minimale Ersatzdateien (in diesem Ordner) und kompiliert Vertex- und
Fragment-Programm mit glslangValidator im HLSL-Modus nach SPIR-V.

Surface Shader (#pragma surface): Unity erzeugt daraus die eigentlichen Pässe. Hier wird nur die Nutzerfunktion
(surf und ggf. vertex:…) über einen kleinen Rahmen geprüft (SurfaceOutputStandard/Input-Aufbau, Typen, Aufrufe).
Die generierten Licht-, Schatten- und Nebel-Pässe prüft das NICHT.

Ersetzt NICHT den Unity-Shader-Compiler (Plattform-Backends, Varianten, ShaderLab-Syntax werden nicht geprüft).

Aufruf: python3 Tools/ShaderCheck/check_shaders.py [Shader-Dateien …]
Voraussetzung: glslangValidator (Paket glslang-tools).
"""
import os, re, subprocess, sys, tempfile, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

SURF_WRAPPER = """
struct RP_CheckV2F { float4 pos : SV_POSITION; };
RP_CheckV2F rp_check_vert(appdata_full v)
{
    RP_CheckV2F o;
    %(vertcall)s
    o.pos = UnityObjectToClipPos(v.vertex);
    return o;
}
float4 rp_check_frag(RP_CheckV2F i) : SV_Target
{
    Input IN = (Input)0;
    %(outtype)s o = (%(outtype)s)0;
    surf(IN, o);
    return float4(o.Albedo + o.Emission, o.Alpha);
}
"""


def compile_stage(path, bi, body, stage, entry):
    with tempfile.NamedTemporaryFile("w", suffix=".hlsl", delete=False) as f:
        f.write(body)
        tmp = f.name
    cmd = ["glslangValidator", "-D", "-V", "-S", stage, "-e", entry, "--hlsl-iomap", "--auto-map-bindings", "-I" + HERE, "-o", os.devnull, tmp]
    r = subprocess.run(cmd, capture_output=True, text=True)
    out = (r.stdout + r.stderr).replace(tmp, os.path.basename(path) + "#" + str(bi))
    status = "OK" if r.returncode == 0 else "FEHLER"
    print("%-8s %s  Block %d  %s (%s)" % (status, os.path.relpath(path, ROOT), bi, stage, entry))
    if r.returncode != 0:
        print("\n".join("    " + l for l in out.strip().splitlines() if l.strip()))
    os.unlink(tmp)
    return r.returncode == 0


def check(path):
    src = open(path, encoding="utf-8").read()
    includes = "\n".join(re.findall(r"CGINCLUDE(.*?)ENDCG", src, re.S))
    blocks = re.findall(r"CGPROGRAM(.*?)ENDCG", src, re.S)
    ok = True
    for bi, block in enumerate(blocks):
        full = includes + "\n" + block
        vert = re.search(r"#pragma\s+vertex\s+(\w+)", full)
        frag = re.search(r"#pragma\s+fragment\s+(\w+)", full)
        surf = re.search(r"#pragma\s+surface\s+(\w+)\s+(\w+)([^\n]*)", full)
        body = re.sub(r"#pragma[^\n]*", "", full)
        if surf:
            opts = surf.group(3)
            vm = re.search(r"vertex:(\w+)", opts)
            # vertex:vert kann (inout appdata_full) oder (inout appdata_full, out Input) haben
            vertcall = ""
            if vm:
                sig = re.search(r"void\s+" + vm.group(1) + r"\s*\(([^)]*)\)", body)
                vertcall = ("Input vo; " + vm.group(1) + "(v, vo);") if sig and "Input" in sig.group(1) else (vm.group(1) + "(v);")
            model = surf.group(2)
            outtype = {"Standard": "SurfaceOutputStandard", "StandardSpecular": "SurfaceOutputStandardSpecular"}.get(model, "SurfaceOutput")
            body = '#include "Lighting.cginc"\n' + body + SURF_WRAPPER % {"vertcall": vertcall, "outtype": outtype}
            ok &= compile_stage(path, bi, body, "vert", "rp_check_vert")
            ok &= compile_stage(path, bi, body, "frag", "rp_check_frag")
            continue
        for stage, m in (("vert", vert), ("frag", frag)):
            if not m:
                continue
            ok &= compile_stage(path, bi, body, stage, m.group(1))
    return ok


def main():
    files = sys.argv[1:] or glob.glob(os.path.join(ROOT, "RePlanet", "Assets", "**", "*.shader"), recursive=True)
    results = [check(f) for f in sorted(files)]
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
