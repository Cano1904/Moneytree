using System;
using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Kamera: Third-Person mit Kollision, Menü-Kamerafahrt, Bauansicht, Fotomodus (freie Kamera, echter PNG-Export,
    /// Hochformat 9:16), Zwischensequenzen (Übersteuerung) und Render-Skalierung.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public Camera Cam { get; private set; }
        /// <summary>Eigene Nachbearbeitung (Bloom, Tonemapping, Luftperspektive …) an der Hauptkamera.</summary>
        public PostFX Post { get; private set; }
        public float Yaw, Pitch = 18f, Distance = 6.5f;
        /// <summary>Zwischensequenz steuert die Kamera direkt.</summary>
        public bool Cinematic;
        float shake, menuT;
        Vector3 photoPos; float photoYaw, photoPitch; bool photoInit;
        RenderTexture scaled;
        readonly List<Box> tmp = new List<Box>();

        void Awake()
        {
            I = this;
            DisableSceneLeftovers();
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            Cam = go.AddComponent<Camera>();
            Cam.nearClipPlane = 0.15f;
            Cam.farClipPlane = 800f;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.allowMSAA = true;
            Cam.allowHDR = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<FlareLayer>();
            Post = go.AddComponent<PostFX>();
            // Weitere Darstellungsbausteine gehören an das Hauptobjekt
            foreach (var t in new[] { typeof(Atmosphere), typeof(TrashRenderer), typeof(ActorsView), typeof(FxView), typeof(FloraRenderer) })
                if (GetComponent(t) == null) gameObject.AddComponent(t);
        }

        /// <summary>Kameras, Listener und Richtungslichter aus einer Beispielszene würden doppelt rendern/beleuchten.</summary>
        void DisableSceneLeftovers()
        {
            try
            {
                foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c != null && c.gameObject.scene.IsValid() && c.hideFlags == HideFlags.None && c.transform.root != transform.root) c.gameObject.SetActive(false);
                foreach (var l in Resources.FindObjectsOfTypeAll<AudioListener>())
                    if (l != null && l.gameObject.scene.IsValid() && l.hideFlags == HideFlags.None && l.transform.root != transform.root) l.enabled = false;
                foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                    if (l != null && l.type == LightType.Directional && l.gameObject.scene.IsValid() && l.hideFlags == HideFlags.None && l.transform.root != transform.root) l.gameObject.SetActive(false);
            }
            catch (Exception e) { Debug.LogWarning("[RE:PLANET] Szenenobjekte: " + e.Message); }
        }

        public void Shake(float amount)
        {
            if (GameApp.I != null && !GameApp.I.Settings.CameraShake) return;
            shake = Mathf.Max(shake, amount);
        }

        void LateUpdate()
        {
            var app = GameApp.I;
            if (app == null) return;
            Cam.fieldOfView = PhotoMode.Active ? PhotoMode.Fov : app.Settings.Fov;
            if (Post != null) Post.Configure(app.Settings.Quality, app.Settings.ReduceFlashing);
            Hud.CameraYaw = Yaw;
            if (Cinematic) { ApplyRenderScale(app); return; }
            if (app.Mode == AppMode.Menu || app.Mode == AppMode.PlanetSelect || (app.Mode == AppMode.Loading && app.W == null)) { MenuCamera(); ApplyRenderScale(app); return; }
            if (!app.InGame || PlayerController.I == null) { ApplyRenderScale(app); return; }

            if (PhotoMode.Active) { PhotoCamera(); ApplyRenderScale(app); HandleCapture(app); return; }
            photoInit = false;
            var target = PlayerController.I.RenderPos + Vector3.up * (PlayerController.I.InVehicle ? 2.4f : 1.4f);
            bool look = !UIState.BlocksGameplay && !BuildMode.Active;
            if (look)
            {
                var d = InputMap.Look(app.Settings.MouseSensitivity, app.Settings.PadSensitivity, app.Settings.InvertY);
                Yaw += d.x;
                Pitch = Mathf.Clamp(Pitch - d.y, -25f, 70f);
                float sc = InputMap.Scroll();
                if (Mathf.Abs(sc) > 0.001f) Distance = Mathf.Clamp(Distance - sc * 6f, 3f, PlayerController.I.InVehicle ? 18f : 12f);
            }
            if (PlayerController.I.InVehicle && Distance < 8f) Distance = Mathf.Lerp(Distance, 9f, Time.deltaTime * 2f);
            float dist = Distance;
            float pitch = Pitch;
            if (BuildMode.Active) { pitch = 55f; dist = 26f; target = new Vector3(0, target.y, -104); }
            var rot = Quaternion.Euler(pitch, Yaw, 0);
            var wanted = target - rot * Vector3.forward * dist;
            wanted = Collide(target, wanted);
            var pos = wanted;
            if (shake > 0)
            {
                shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.5f);
                pos += UnityEngine.Random.insideUnitSphere * shake * 0.35f;
            }
            Cam.transform.position = pos;
            Cam.transform.rotation = Quaternion.LookRotation(target - pos);
            ApplyRenderScale(app);
        }

        /// <summary>Hält die Kamera vor Wänden und über dem Gelände.</summary>
        Vector3 Collide(Vector3 from, Vector3 to)
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null) return to;
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return to;
            dir /= len;
            float safe = len;
            for (float t = 0.5f; t <= len; t += 0.4f)
            {
                var p = from + dir * t;
                float g = Terrain.HeightAt(wv.Planet, p.x, p.z);
                if (p.y < g + 0.35f && !(Terrain.WaterLevel(wv.Planet) > -50 && p.y < Terrain.WaterLevel(wv.Planet))) { safe = t - 0.4f; break; }
                wv.Layout.Query(p.x, p.z, 0.3f, tmp);
                bool hit = false;
                foreach (var b in tmp)
                {
                    if (!b.Solid || b.Kind == "gateblock" || b.DuneSet >= 0) continue;
                    if (b.Contains(p.x, p.z, 0.25f) && p.y > b.Y0 && p.y < b.Y0 + b.H) { hit = true; break; }
                }
                if (hit) { safe = t - 0.4f; break; }
            }
            safe = Mathf.Max(1.2f, safe);
            var res = from + dir * safe;
            float gy = Terrain.HeightAt(wv.Planet, res.x, res.z);
            if (res.y < gy + 0.35f && !(Terrain.WaterLevel(wv.Planet) > -50 && from.y < Terrain.WaterLevel(wv.Planet))) res.y = gy + 0.35f;
            return res;
        }

        void MenuCamera()
        {
            var wv = WorldView.I;
            if (wv == null || wv.Layout == null) return;
            menuT += Time.unscaledDeltaTime * 0.03f;
            var b = wv.Layout.Base;
            var center = new Vector3(b.Spawn.x + 3, b.Spawn.y + 1.2f, b.Spawn.z + 14);
            float a = Mathf.Sin(menuT) * 0.6f + Mathf.PI * 1.15f;
            var pos = center + new Vector3(Mathf.Cos(a) * 9f, 2.2f + Mathf.Sin(menuT * 1.7f) * 0.6f, Mathf.Sin(a) * 9f);
            Cam.transform.position = pos;
            Cam.transform.rotation = Quaternion.LookRotation((center + new Vector3(0, 6f, 0) + Vector3.forward * 30f) - pos);
            if (Atmosphere.I != null) { Atmosphere.I.ForcePhase = 0.72f; }
        }

        void Update()
        {
            if (Atmosphere.I != null && GameApp.I != null && GameApp.I.Mode != AppMode.Menu && GameApp.I.Mode != AppMode.PlanetSelect && !Cinematic)
                Atmosphere.I.ForcePhase = -1f;
        }

        // ------------------------------------------------------------ Fotomodus
        void PhotoCamera()
        {
            if (!photoInit)
            {
                photoInit = true;
                photoPos = Cam.transform.position;
                var e = Cam.transform.rotation.eulerAngles;
                photoYaw = e.y; photoPitch = e.x > 180 ? e.x - 360 : e.x;
            }
            if (!string.IsNullOrEmpty(PhotoMode.JumpToViewpoint) && WorldView.I != null && WorldView.I.Layout != null)
            {
                var s = WorldView.I.Layout.FindSpot(PhotoMode.JumpToViewpoint);
                if (s != null) { photoPos = new Vector3(s.Pos.x, s.Pos.y, s.Pos.z); photoYaw = s.Yaw * Mathf.Rad2Deg; photoPitch = 8f; }
                PhotoMode.JumpToViewpoint = null;
            }
            var app = GameApp.I;
            if (!UIRoot.WantsCursor)
            {
                var d = InputMap.Look(app.Settings.MouseSensitivity, app.Settings.PadSensitivity, app.Settings.InvertY);
                photoYaw += d.x; photoPitch = Mathf.Clamp(photoPitch - d.y, -85f, 85f);
            }
            var mv = InputMap.Move();
            float up = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            float speed = (InputMap.Held(GameAction.Sprint) ? 18f : 6f) * Time.unscaledDeltaTime;
            var rot = Quaternion.Euler(photoPitch, photoYaw, 0);
            photoPos += (rot * new Vector3(mv.x, 0, mv.y) + Vector3.up * up) * speed;
            // Fotokamera bleibt in der Nähe des Spielers (max. 60 m), damit sie nicht aus der Welt fliegt
            if (PlayerController.I != null)
            {
                var off = photoPos - PlayerController.I.RenderPos;
                if (off.magnitude > 60f) photoPos = PlayerController.I.RenderPos + off.normalized * 60f;
            }
            if (WorldView.I != null) photoPos.y = Mathf.Max(photoPos.y, Terrain.HeightAt(WorldView.I.Planet, photoPos.x, photoPos.z) + 0.3f);
            Cam.transform.position = photoPos;
            Cam.transform.rotation = rot * Quaternion.Euler(0, 0, PhotoMode.Roll);
        }

        void HandleCapture(GameApp app)
        {
            if (!PhotoMode.RequestCapture) return;
            PhotoMode.RequestCapture = false;
            try
            {
                int w = PhotoMode.Portrait ? 1080 : 1920, h = PhotoMode.Portrait ? 1920 : 1080;
                // HDR-Ziel, damit Bloom/Tonemapping wie im Spiel wirken; danach in ein 8-Bit-Ziel kopieren (ReadPixels-sicher)
                var hdrFmt = HdrFormat();
                var rtHdr = RenderTexture.GetTemporary(w, h, 24, hdrFmt, hdrFmt == RenderTextureFormat.ARGB32 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear, Mathf.Max(1, QualitySettings.antiAliasing));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = Cam.targetTexture;
                Cam.targetTexture = rtHdr;
                Cam.Render();
                Cam.targetTexture = prev;
                Graphics.Blit(rtHdr, rt);
                RenderTexture.ReleaseTemporary(rtHdr);
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                var png = tex.EncodeToPNG();
                Destroy(tex);
                Directory.CreateDirectory(app.PhotoDir);
                string planet = app.W != null ? app.W.CurrentPlanet : "welt";
                string file = Path.Combine(app.PhotoDir, "RePlanet_" + planet + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + (PhotoMode.ShowBefore ? "_vorher" : "") + (PhotoMode.Portrait ? "_hochformat" : "") + ".png");
                File.WriteAllBytes(file, png);
                PhotoMode.LastSavedPath = file;
                Hud.Show("Foto gespeichert: " + file, ToastKind.Success, 5f);
                AudioManager.Play("ui_click");
            }
            catch (Exception e)
            {
                Hud.Show("Foto konnte nicht gespeichert werden: " + e.Message, ToastKind.Error, 6f);
            }
        }

        // ------------------------------------------------------------ Render-Skalierung
        void ApplyRenderScale(GameApp app)
        {
            float s = app.Settings.RenderScale;
            if (s >= 0.99f || PhotoMode.RequestCapture)
            {
                if (Cam.targetTexture != null) { Cam.targetTexture = null; }
                if (scaled != null) { scaled.Release(); Destroy(scaled); scaled = null; }
                return;
            }
            int w = Mathf.Max(320, (int)(Screen.width * s)), h = Mathf.Max(180, (int)(Screen.height * s));
            if (scaled == null || scaled.width != w || scaled.height != h)
            {
                if (scaled != null) { Cam.targetTexture = null; scaled.Release(); Destroy(scaled); }
                scaled = new RenderTexture(w, h, 24, HdrFormat()) { filterMode = FilterMode.Bilinear, antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
                scaled.Create();
            }
            Cam.targetTexture = scaled;
        }

        /// <summary>HDR-Format für Renderziele (sonst gingen Bloom-Spitzen und Tonemapping bei Render-Skalierung verloren).</summary>
        static RenderTextureFormat HdrFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)) return RenderTextureFormat.DefaultHDR;
            return RenderTextureFormat.ARGB32;
        }

        void OnGUI()
        {
            if (scaled == null || Cam.targetTexture != scaled || Event.current.type != EventType.Repaint) return;
            GUI.depth = 1000; // unter der Oberfläche
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), scaled, ScaleMode.StretchToFill, false);
        }
    }
}
