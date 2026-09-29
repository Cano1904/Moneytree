using System;
using System.IO;
using System.Text.Json;
using Glasscore.Simulation;
using Raylib_cs;

namespace Glasscore.Desktop
{
    public enum Bind
    {
        Forward, Back, Left, Right, Jump, Fire, Anchor, NextWeapon, PrevWeapon, Pause,
    }

    /// <summary>Settings + player profile, persisted as JSON in %APPDATA%\GLASSCORE.</summary>
    public sealed class DesktopConfig
    {
        public static readonly (int W, int H)[] WindowSizes = { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160) };
        public static readonly string[] DisplayModes = { "Windowed", "Borderless Window", "Fullscreen" };
        public static readonly string[] QualityNames = { "Low - pre-fractured chunks", "Medium - real-time 20 pieces", "High - full Voronoi 100+" };

        // Graphics
        public int WindowSize { get; set; } = 1;
        public int DisplayMode { get; set; }
        public int FractureQuality { get; set; } = 1;
        public bool VSync { get; set; } = true;
        public float FieldOfView { get; set; } = 90f;

        // Audio
        public float MasterVolume { get; set; } = 0.8f;
        public float SfxVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 0.5f;

        // Controls: >= 0 keyboard key, -1 left mouse, -2 right mouse, -3 middle mouse
        public int[] Bindings { get; set; } = DefaultBindings();
        public float MouseSensitivity { get; set; } = 1f;
        public float GamepadLookSpeed { get; set; } = 180f;
        public bool InvertY { get; set; }

        // Online / profile
        public string ServerHosts { get; set; } = "";
        public string PlayerName { get; set; } = "Glazier" + new Random().Next(100, 999);
        public string PlayerKey { get; set; } = Guid.NewGuid().ToString("N");
        public int Rating { get; set; } = RatingSystem.StartingRating;
        public int[] WeaponSkins { get; set; } = new int[4];
        public int Trail { get; set; } = 1;

        public static int[] DefaultBindings() => new[]
        {
            (int)KeyboardKey.W, (int)KeyboardKey.S, (int)KeyboardKey.A, (int)KeyboardKey.D, (int)KeyboardKey.Space,
            -1, -2, (int)KeyboardKey.E, (int)KeyboardKey.Q, (int)KeyboardKey.Escape,
        };

        public static string Directory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(root)) root = AppContext.BaseDirectory;
                return Path.Combine(root, "GLASSCORE");
            }
        }

        private static string FilePath => Path.Combine(Directory, "settings.json");

        public static DesktopConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var c = JsonSerializer.Deserialize<DesktopConfig>(File.ReadAllText(FilePath));
                    if (c != null) { c.Validate(); return c; }
                }
            }
            catch (Exception) { /* corrupt file: fall back to defaults */ }
            var d = new DesktopConfig();
            d.Validate();
            return d;
        }

        public void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception) { }
        }

        public void Validate()
        {
            if (Bindings == null || Bindings.Length != Enum.GetValues(typeof(Bind)).Length) Bindings = DefaultBindings();
            if (WeaponSkins == null || WeaponSkins.Length != WeaponCatalog.Count) WeaponSkins = new int[WeaponCatalog.Count];
            if (string.IsNullOrEmpty(PlayerKey)) PlayerKey = Guid.NewGuid().ToString("N");
            PlayerName = LobbyRules.SanitizePlayerName(PlayerName);
            WindowSize = Math.Clamp(WindowSize, 0, WindowSizes.Length - 1);
            DisplayMode = Math.Clamp(DisplayMode, 0, DisplayModes.Length - 1);
            FractureQuality = Math.Clamp(FractureQuality, 0, 2);
            Trail = Math.Clamp(Trail, 0, Palette.Trails.Length - 1);
            for (int i = 0; i < WeaponSkins.Length; i++) WeaponSkins[i] = Math.Clamp(WeaponSkins[i], 0, Palette.Skins.Length - 1);
        }

        public string[] ServerHostList => ServerHosts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        public void ApplyAudio()
        {
            Sfx.SfxGain = MasterVolume * SfxVolume;
            Sfx.MusicGain = MasterVolume * MusicVolume;
        }

        public (int sites, bool prebaked, int maxShards, int crumble) Fracture =>
            FractureQuality == 0 ? (12, true, 96, 8) : FractureQuality == 1 ? (20, false, 400, 12) : (112, false, 1400, 16);

        public static string BindLabel(int code)
        {
            switch (code)
            {
                case -1: return "Left Click";
                case -2: return "Right Click";
                case -3: return "Middle Click";
                case 0: return "-";
                default: return ((KeyboardKey)code).ToString();
            }
        }

        public static string BindName(Bind b)
        {
            switch (b)
            {
                case Bind.Forward: return "Move Forward";
                case Bind.Back: return "Move Back";
                case Bind.Left: return "Move Left";
                case Bind.Right: return "Move Right";
                case Bind.Jump: return "Jump";
                case Bind.Fire: return "Fire Weapon";
                case Bind.Anchor: return "Suction-Boot Anchor";
                case Bind.NextWeapon: return "Next Weapon";
                case Bind.PrevWeapon: return "Previous Weapon";
                default: return "Pause Menu";
            }
        }
    }

    public static class Palette
    {
        public static readonly Color Cyan = new Color(0, 255, 255, 255);
        public static readonly Color Magenta = new Color(255, 64, 153, 255);
        public static readonly Color Green = new Color(77, 255, 115, 255);
        public static readonly Color Red = new Color(255, 77, 77, 255);
        public static readonly Color Gold = new Color(255, 217, 51, 255);
        public static readonly Color Panel = new Color(5, 15, 23, 215);

        public static readonly (string Name, Color Body, Color Glow)[] Skins =
        {
            ("Rohstein", new Color(115, 115, 122, 255), new Color(0, 255, 255, 255)),
            ("Neon Cyan", new Color(13, 64, 77, 255), new Color(0, 255, 255, 255)),
            ("Magma", new Color(64, 13, 5, 255), new Color(255, 90, 0, 255)),
            ("Amethyst", new Color(51, 13, 77, 255), new Color(180, 50, 255, 255)),
            ("Gold Leaf", new Color(140, 107, 25, 255), new Color(255, 217, 77, 255)),
            ("Frosted", new Color(204, 230, 255, 255), new Color(153, 230, 255, 255)),
        };

        public static readonly (string Name, Color Color)[] Trails =
        {
            ("None", new Color(0, 0, 0, 0)),
            ("Cyan Pulse", new Color(0, 255, 255, 230)),
            ("Ember", new Color(255, 128, 25, 230)),
            ("Prism", new Color(255, 51, 204, 230)),
            ("Glass Dust", new Color(255, 255, 255, 200)),
        };

        public static Color Slot(int slot)
        {
            switch (slot % 8)
            {
                case 0: return new Color(0, 255, 255, 255);
                case 1: return new Color(255, 64, 140, 255);
                case 2: return new Color(255, 204, 25, 255);
                case 3: return new Color(102, 255, 77, 255);
                case 4: return new Color(166, 102, 255, 255);
                case 5: return new Color(255, 128, 25, 255);
                case 6: return new Color(77, 153, 255, 255);
                default: return new Color(242, 242, 242, 255);
            }
        }

        public static Color GlassTint(GlassType t, float alpha = 1f)
        {
            switch (t)
            {
                case GlassType.Standard: return new Color(140, 217, 255, (int)(70 * alpha));
                case GlassType.Tempered: return new Color(115, 255, 204, (int)(90 * alpha));
                case GlassType.Reinforced: return new Color(90, 128, 255, (int)(120 * alpha));
                default: return new Color(0, 0, 0, 0);
            }
        }

        public static Color GlassEdge(GlassType t)
        {
            switch (t)
            {
                case GlassType.Standard: return new Color(0, 255, 255, 230);
                case GlassType.Tempered: return new Color(51, 255, 153, 230);
                case GlassType.Reinforced: return new Color(102, 128, 255, 255);
                default: return Color.White;
            }
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.R, c.G, c.B, (int)Math.Clamp(a * 255f, 0f, 255f));
        public static Color Scale(Color c, float k) => new Color((int)Math.Min(255, c.R * k), (int)Math.Min(255, c.G * k), (int)Math.Min(255, c.B * k), (int)c.A);
    }
}
