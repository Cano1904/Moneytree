using System;
using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Alle Einstellungen (Grafik, Audio, Steuerung, Barrierefreiheit). Gespeichert als settings.json.</summary>
    public class Settings
    {
        // ------------------------------------------------------------ Grafik
        public int Quality = 2;            // 0 Niedrig, 1 Mittel, 2 Hoch, 3 Ultra
        public int ResWidth, ResHeight;    // 0 = aktuelle Bildschirmauflösung
        public int WindowMode = 0;         // 0 Vollbild (randlos), 1 exklusives Vollbild, 2 Fenster
        public bool VSync = true;
        public int FpsLimit = 0;           // 0 = unbegrenzt
        public int Shadows = 2;            // 0 aus, 1 niedrig, 2 mittel, 3 hoch
        public int AntiAliasing = 2;       // 0, 2, 4, 8 (MSAA)
        public float ViewDistance = 1f;    // 0,5 … 1,5
        public float RenderScale = 1f;     // 0,5 … 1,0
        public int Particles = 2;          // 0 wenig, 1 mittel, 2 viel
        public float Brightness = 1f;      // 0,7 … 1,3
        public float Fov = 60f;
        public bool ShowFps;

        // ------------------------------------------------------------ Audio
        public float MasterVolume = 0.9f, MusicVolume = 0.7f, SfxVolume = 0.85f, AmbientVolume = 0.7f, UiVolume = 0.6f, VoiceVolume = 0.8f;
        public bool MuteWhenUnfocused = true;

        // ------------------------------------------------------------ Steuerung
        public float MouseSensitivity = 1f, PadSensitivity = 1f;
        public bool InvertY;
        /// <summary>true = Werkzeuge halten, false = Umschalten (einmal drücken startet, erneut drücken stoppt).</summary>
        public bool HoldActions = true;
        public Dictionary<string, string> Bindings = new Dictionary<string, string>();

        // ------------------------------------------------------------ Barrierefreiheit
        public bool Subtitles = true;
        public float TextScale = 1f;       // 0,8 … 1,6
        public bool CameraShake = true;
        public bool HighContrast;
        public bool ReduceFlashing;

        // ------------------------------------------------------------ Sonstiges
        public string Language = "de";
        public string PlayerName = "MIKO";
        public int CoopPort = 7777;
        public string LastJoin = "";
        public bool IntroSeenOnce;

        public static readonly string[] QualityNames = { "Niedrig", "Mittel", "Hoch", "Ultra" };
        public static readonly string[] ShadowNames = { "Aus", "Niedrig", "Mittel", "Hoch" };
        public static readonly string[] WindowModeNames = { "Vollbild (randlos)", "Exklusives Vollbild", "Fenster" };
        public static readonly int[] FpsOptions = { 0, 30, 60, 90, 120, 144 };
        public static readonly int[] AaOptions = { 0, 2, 4, 8 };

        static string PathFile { get { return Path.Combine(Application.persistentDataPath, "settings.json"); } }

        /// <summary>Wendet die Voreinstellung einer Qualitätsstufe auf die Einzelwerte an.</summary>
        public void ApplyPreset(int q)
        {
            Quality = Mathf.Clamp(q, 0, 3);
            Shadows = new[] { 0, 1, 2, 3 }[Quality];
            AntiAliasing = new[] { 0, 2, 4, 8 }[Quality];
            ViewDistance = new[] { 0.6f, 0.85f, 1f, 1.3f }[Quality];
            Particles = new[] { 0, 1, 2, 2 }[Quality];
            RenderScale = new[] { 0.75f, 0.9f, 1f, 1f }[Quality];
        }

        public void Apply()
        {
            Loc.Lang = Language;
            try
            {
                // Qualitätsstufe (falls die Unity-Stufen existieren) + Einzelwerte
                int levels = QualitySettings.names.Length;
                if (levels > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(Mathf.RoundToInt(Quality / 3f * (levels - 1)), 0, levels - 1), false);
                QualitySettings.vSyncCount = VSync ? 1 : 0;
                Application.targetFrameRate = VSync || FpsLimit <= 0 ? -1 : FpsLimit;
                QualitySettings.antiAliasing = AntiAliasing;
                switch (Shadows)
                {
                    case 0: QualitySettings.shadows = ShadowQuality.Disable; break;
                    case 1: QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowResolution = ShadowResolution.Low; QualitySettings.shadowDistance = 45f * ViewDistance; break;
                    case 2: QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.shadowDistance = 70f * ViewDistance; break;
                    default: QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.VeryHigh; QualitySettings.shadowDistance = 110f * ViewDistance; break;
                }
                QualitySettings.shadowCascades = Shadows >= 2 ? 2 : 1;
                QualitySettings.lodBias = 0.7f + ViewDistance * 0.6f;
                if (!Application.isEditor)
                {
                    var mode = WindowMode == 0 ? FullScreenMode.FullScreenWindow : WindowMode == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
                    int w = ResWidth > 0 ? ResWidth : Screen.currentResolution.width;
                    int h = ResHeight > 0 ? ResHeight : Screen.currentResolution.height;
                    if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
                }
            }
            catch (Exception e) { Debug.LogWarning("Grafikeinstellungen: " + e.Message); }
            AudioListener.volume = MasterVolume;
            InputMap.LoadBindings(Bindings);
        }

        public void Save()
        {
            try
            {
                Bindings = InputMap.SaveBindings();
                var o = new JObj()
                    .Set("q", Quality).Set("rw", ResWidth).Set("rh", ResHeight).Set("wm", WindowMode).Set("vs", VSync).Set("fps", FpsLimit)
                    .Set("sh", Shadows).Set("aa", AntiAliasing).Set("vd", ViewDistance).Set("rs", RenderScale).Set("pa", Particles).Set("br", Brightness)
                    .Set("fov", Fov).Set("sf", ShowFps)
                    .Set("mv", MasterVolume).Set("mu", MusicVolume).Set("sx", SfxVolume).Set("am", AmbientVolume).Set("ui", UiVolume).Set("vo", VoiceVolume).Set("mf", MuteWhenUnfocused)
                    .Set("ms", MouseSensitivity).Set("ps", PadSensitivity).Set("iy", InvertY).Set("ha", HoldActions)
                    .Set("st", Subtitles).Set("ts", TextScale).Set("cs", CameraShake).Set("hc", HighContrast).Set("rf", ReduceFlashing)
                    .Set("lang", Language).Set("name", PlayerName).Set("port", CoopPort).Set("lj", LastJoin).Set("iso", IntroSeenOnce);
                var b = new JObj();
                foreach (var kv in Bindings) b[kv.Key] = kv.Value;
                o["bind"] = b;
                File.WriteAllText(PathFile, Json.Write(o));
            }
            catch (Exception e) { Debug.LogWarning("Einstellungen konnten nicht gespeichert werden: " + e.Message); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(PathFile)) { s.ApplyPreset(2); return s; }
                JObj o;
                if (!Json.TryParseObj(File.ReadAllText(PathFile), out o)) return s;
                s.Quality = o.Int("q", 2); s.ResWidth = o.Int("rw"); s.ResHeight = o.Int("rh"); s.WindowMode = o.Int("wm"); s.VSync = o.Bool("vs", true); s.FpsLimit = o.Int("fps");
                s.Shadows = o.Int("sh", 2); s.AntiAliasing = o.Int("aa", 2); s.ViewDistance = Mathf.Clamp(o.Float("vd", 1f), 0.5f, 1.5f); s.RenderScale = Mathf.Clamp(o.Float("rs", 1f), 0.5f, 1f);
                s.Particles = o.Int("pa", 2); s.Brightness = Mathf.Clamp(o.Float("br", 1f), 0.7f, 1.3f); s.Fov = Mathf.Clamp(o.Float("fov", 60f), 45f, 90f); s.ShowFps = o.Bool("sf");
                s.MasterVolume = o.Float("mv", 0.9f); s.MusicVolume = o.Float("mu", 0.7f); s.SfxVolume = o.Float("sx", 0.85f); s.AmbientVolume = o.Float("am", 0.7f); s.UiVolume = o.Float("ui", 0.6f); s.VoiceVolume = o.Float("vo", 0.8f);
                s.MuteWhenUnfocused = o.Bool("mf", true);
                s.MouseSensitivity = Mathf.Clamp(o.Float("ms", 1f), 0.1f, 4f); s.PadSensitivity = Mathf.Clamp(o.Float("ps", 1f), 0.1f, 4f); s.InvertY = o.Bool("iy"); s.HoldActions = o.Bool("ha", true);
                s.Subtitles = o.Bool("st", true); s.TextScale = Mathf.Clamp(o.Float("ts", 1f), 0.8f, 1.6f); s.CameraShake = o.Bool("cs", true); s.HighContrast = o.Bool("hc"); s.ReduceFlashing = o.Bool("rf");
                s.Language = o.Str("lang", "de"); s.PlayerName = o.Str("name", "MIKO"); s.CoopPort = Mathf.Clamp(o.Int("port", 7777), 1024, 65535); s.LastJoin = o.Str("lj", ""); s.IntroSeenOnce = o.Bool("iso");
                var b = o.Obj("bind");
                if (b != null) foreach (var kv in b) if (kv.Value is string) s.Bindings[kv.Key] = (string)kv.Value;
            }
            catch (Exception e) { Debug.LogWarning("Einstellungen beschädigt – Standardwerte: " + e.Message); }
            return s;
        }
    }

    /// <summary>Persönliches Spielerprofil (Name, Kosmetik, persönliche Freischaltungen, Wiederverbindungs-Token).</summary>
    public class Profile
    {
        public string Id;
        public string Color = "c_tuerkis", Accent = "a_orange", Sticker = "s_none", Attach = "x_none";
        public HashSet<string> Unlocks = new HashSet<string>();
        public Dictionary<string, string> Tokens = new Dictionary<string, string>();

        static string PathFile { get { return Path.Combine(Application.persistentDataPath, "profile.json"); } }

        public JObj CosmeticsJson()
        {
            return new JObj().Set("color", Color).Set("accent", Accent).Set("sticker", Sticker).Set("attach", Attach);
        }

        public void Save()
        {
            try
            {
                var tok = new JObj();
                foreach (var kv in Tokens) tok[kv.Key] = kv.Value;
                var o = new JObj().Set("id", Id).Set("c", Color).Set("a", Accent).Set("s", Sticker).Set("x", Attach).Set("u", new List<object>(Unlocks)).Set("t", tok);
                File.WriteAllText(PathFile, Json.Write(o));
            }
            catch (Exception e) { Debug.LogWarning("Profil konnte nicht gespeichert werden: " + e.Message); }
        }

        public static Profile Load()
        {
            var p = new Profile();
            try
            {
                JObj o;
                if (File.Exists(PathFile) && Json.TryParseObj(File.ReadAllText(PathFile), out o))
                {
                    p.Id = o.Str("id");
                    p.Color = o.Str("c", p.Color); p.Accent = o.Str("a", p.Accent); p.Sticker = o.Str("s", p.Sticker); p.Attach = o.Str("x", p.Attach);
                    foreach (var u in o.Strs("u")) p.Unlocks.Add(u);
                    var t = o.Obj("t");
                    if (t != null) foreach (var kv in t) if (kv.Value is string) p.Tokens[kv.Key] = (string)kv.Value;
                }
            }
            catch (Exception e) { Debug.LogWarning("Profil beschädigt: " + e.Message); }
            if (string.IsNullOrEmpty(p.Id)) { p.Id = Guid.NewGuid().ToString("N"); p.Save(); }
            return p;
        }
    }
}
