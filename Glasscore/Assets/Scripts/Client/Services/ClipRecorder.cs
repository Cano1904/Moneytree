using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Glasscore.Client
{
    /// <summary>
    /// Social clip export. Keeps a rolling 30 s buffer of 640×360 JPEG frames (15 fps, ~12 MB) captured
    /// asynchronously from the game camera (HUD-free). Server highlight markers are mapped to local time;
    /// exporting writes the frame sequence and, if ffmpeg is on PATH, an H.264 MP4 next to it.
    /// </summary>
    public sealed class ClipRecorder : IDisposable
    {
        public const int Width = 640;
        public const int Height = 360;
        public const int Fps = 15;
        public const float BufferSeconds = 30f;

        private struct Frame
        {
            public double Time;
            public byte[] Jpeg;
        }

        public struct Marker
        {
            public double Time;
            public string Label;
        }

        private readonly Frame[] _frames = new Frame[(int)(Fps * BufferSeconds)];
        private readonly object _lock = new object();
        private readonly List<Marker> _markers = new List<Marker>();
        private int _head;
        private RenderTexture _rt;
        private double _nextCapture;
        private int _inFlight;

        public bool Enabled = true;
        public IReadOnlyList<Marker> Markers => _markers;
        public string LastExportPath { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public static string ClipDirectory => Path.Combine(Application.persistentDataPath, "Clips");

        public void Capture(RenderTexture source, double now)
        {
            if (!Enabled || !SystemInfo.supportsAsyncGPUReadback || now < _nextCapture || _inFlight > 4) return;
            _nextCapture = now + 1.0 / Fps;
            if (_rt == null)
            {
                _rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) { name = "ClipCapture" };
                _rt.Create();
            }
            Graphics.Blit(source, _rt);
            _inFlight++;
            double stamp = now;
            AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, req => OnReadback(req, stamp));
        }

        private void OnReadback(AsyncGPUReadbackRequest req, double stamp)
        {
            if (req.hasError)
            {
                _inFlight--;
                return;
            }
            byte[] raw = req.GetData<byte>().ToArray();
            Task.Run(() =>
            {
                try
                {
                    byte[] jpg = ImageConversion.EncodeArrayToJPG(raw, GraphicsFormat.R8G8B8A8_UNorm, Width, Height, 0, 72);
                    lock (_lock)
                    {
                        _frames[_head] = new Frame { Time = stamp, Jpeg = jpg };
                        _head = (_head + 1) % _frames.Length;
                    }
                }
                catch (Exception) { /* encoder unavailable on this platform */ }
                finally { System.Threading.Interlocked.Decrement(ref _inFlight); }
            });
        }

        public void ClearMarkers() => _markers.Clear();

        public void Mark(double localTime, string label) => _markers.Add(new Marker { Time = localTime, Label = label });

        /// <summary>Exports [time - 8 s, time + 2 s]. Returns the output folder (or null when empty).</summary>
        public string Export(double centerTime, string label)
        {
            var selected = new List<Frame>();
            lock (_lock)
            {
                foreach (var f in _frames)
                    if (f.Jpeg != null && f.Time >= centerTime - 8.0 && f.Time <= centerTime + 2.0) selected.Add(f);
            }
            if (selected.Count == 0)
            {
                Status = "Nothing recorded for that moment.";
                return null;
            }
            selected.Sort((a, b) => a.Time.CompareTo(b.Time));

            string safe = MakeSafe(label);
            string dir = Path.Combine(ClipDirectory, $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < selected.Count; i++)
                File.WriteAllBytes(Path.Combine(dir, $"frame_{i:0000}.jpg"), selected[i].Jpeg);

            LastExportPath = dir;
            Status = $"Saved {selected.Count} frames → {dir}";
            TryEncodeMp4(dir, safe);
            return dir;
        }

        private void TryEncodeMp4(string dir, string name)
        {
            try
            {
                var psi = new ProcessStartInfo("ffmpeg", $"-y -loglevel error -framerate {Fps} -i frame_%04d.jpg -c:v libx264 -pix_fmt yuv420p -movflags +faststart \"{name}.mp4\"")
                {
                    WorkingDirectory = dir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var p = Process.Start(psi);
                if (p == null) return;
                p.EnableRaisingEvents = true;
                p.Exited += (_, __) =>
                {
                    if (p.ExitCode == 0) Status = $"Clip exported: {Path.Combine(dir, name + ".mp4")}";
                };
            }
            catch (Exception)
            {
                Status += " (install ffmpeg for automatic MP4 export)";
            }
        }

        public static void OpenClipFolder()
        {
            Directory.CreateDirectory(ClipDirectory);
            Application.OpenURL("file:///" + ClipDirectory.Replace('\\', '/'));
        }

        private static string MakeSafe(string s)
        {
            var chars = new List<char>();
            foreach (char c in s ?? "clip")
            {
                if (char.IsLetterOrDigit(c)) chars.Add(c);
                else if (chars.Count > 0 && chars[chars.Count - 1] != '_') chars.Add('_');
            }
            string r = new string(chars.ToArray()).Trim('_');
            return r.Length == 0 ? "clip" : (r.Length > 40 ? r.Substring(0, 40) : r);
        }

        public void Dispose()
        {
            if (_rt != null) _rt.Release();
        }
    }

    /// <summary>
    /// Camera post-processing (built-in pipeline): feeds the clip recorder with the clean game image
    /// and applies the real-time Gaussian blur behind the pause menu.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraPostFx : MonoBehaviour
    {
        public float TargetBlur;
        public ClipRecorder Recorder;
        private float _blur;
        private Material _mat;
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int DarkenId = Shader.PropertyToID("_Darken");

        private void Update()
        {
            _blur = Mathf.MoveTowards(_blur, TargetBlur, Time.unscaledDeltaTime * 4f);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            Recorder?.Capture(src, Time.unscaledTimeAsDouble);
            if (_blur < 0.01f)
            {
                Graphics.Blit(src, dst);
                return;
            }
            if (_mat == null) _mat = Mats.Blur();

            int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
            RenderTexture a = RenderTexture.GetTemporary(w, h, 0, src.format);
            RenderTexture b = RenderTexture.GetTemporary(w, h, 0, src.format);
            Graphics.Blit(src, a);
            _mat.SetFloat(SpreadId, 1f + _blur * 2.5f);
            _mat.SetFloat(DarkenId, 0f);
            for (int i = 0; i < 3; i++)
            {
                _mat.SetVector(DirectionId, new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(a, b, _mat);
                _mat.SetVector(DirectionId, new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(b, a, _mat);
            }
            _mat.SetFloat(DarkenId, _blur * 0.35f);
            _mat.SetVector(DirectionId, new Vector4(0.7f, 0.7f, 0f, 0f));
            Graphics.Blit(a, dst, _mat);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }
}
