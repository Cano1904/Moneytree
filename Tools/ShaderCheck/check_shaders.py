#!/usr/bin/env python3
"""Syntax-/Typprüfung der eigenen Unity-Shader ohne Unity.

Extrahiert jeden CGPROGRAM-Block, ersetzt UnityCG.cginc durch einen minimalen Ersatz (UnityCG.cginc in diesem Ordner)
und kompiliert Vertex- und Fragment-Programm mit glslangValidator im HLSL-Modus nach SPIR-V.
Ersetzt NICHT den Unity-Shader-Compiler (Plattform-Backends, Varianten, ShaderLab-Syntax werden nicht geprüft).

Aufruf: python3 Tools/ShaderCheck/check_shaders.py [Shader-Dateien …]
Voraussetzung: glslangValidator (Paket glslang-tools).
"""
import os, re, subprocess, sys, tempfile, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

def check(path):
    src = open(path, encoding="utf-8").read()
    blocks = re.findall(r"CGPROGRAM(.*?)ENDCG", src, re.S)
    ok = True
    for bi, block in enumerate(blocks):
        vert = re.search(r"#pragma\s+vertex\s+(\w+)", block)
        frag = re.search(r"#pragma\s+fragment\s+(\w+)", block)
        body = re.sub(r"#pragma[^\n]*", "", block)
        with tempfile.NamedTemporaryFile("w", suffix=".hlsl", delete=False) as f:
            f.write(body)
            tmp = f.name
        for stage, m in (("vert", vert), ("frag", frag)):
            if not m:
                continue
            cmd = ["glslangValidator", "-D", "-V", "-S", stage, "-e", m.group(1), "--hlsl-iomap", "--auto-map-bindings", "-I" + HERE, "-o", os.devnull, tmp]
            r = subprocess.run(cmd, capture_output=True, text=True)
            out = (r.stdout + r.stderr).replace(tmp, os.path.basename(path) + "#" + str(bi))
            status = "OK" if r.returncode == 0 else "FEHLER"
            print("%-8s %s  Block %d  %s (%s)" % (status, os.path.relpath(path, ROOT), bi, stage, m.group(1)))
            if r.returncode != 0:
                ok = False
                print("\n".join("    " + l for l in out.strip().splitlines() if l.strip()))
        os.unlink(tmp)
    return ok

def main():
    files = sys.argv[1:] or glob.glob(os.path.join(ROOT, "RePlanet", "Assets", "**", "*.shader"), recursive=True)
    results = [check(f) for f in sorted(files)]
    sys.exit(0 if all(results) else 1)

if __name__ == "__main__":
    main()
