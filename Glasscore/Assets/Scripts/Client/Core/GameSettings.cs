using System;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    public enum FractureQuality
    {
        Low,     // pre-fractured chunks (4 baked patterns, 12 pieces)
        Medium,  // real-time Voronoi, 20 pieces
        High,    // full Voronoi, 112 structural fragments
    }

    public enum DisplayModeOption
    {
        Fullscreen,
        BorderlessWindow,
        Windowed,
    }

    public enum GameAction
    {
        MoveForward,
        MoveBack,
        MoveLeft,
        MoveRight,
        Jump,
        Fire,
        Anchor,
        NextWeapon,
        PrevWeapon,
        PushToTalk,
        MuteMic,
        Pause,
    }

    public readonly struct FractureProfile
    {
        public readonly int SiteCount;
        public readonly bool Prebaked;
        public readonly int MaxActiveFragments;
        public readonly int CrumbleGrid;
        public readonly float FragmentLifetime;

        public FractureProfile(int sites, bool prebaked, int maxActive, int crumbleGrid, float lifetime)
        {
            SiteCount = sites; Prebaked = prebaked; MaxActiveFragments = maxActive; CrumbleGrid = crumbleGrid; FragmentLifetime = lifetime;
        }

        public static FractureProfile For(FractureQuality q)
        {
            switch (q)
            {
                case FractureQuality.Low: return new FractureProfile(12, true, 96, 8, 2.5f);
                case FractureQuality.Medium: return new FractureProfile(20, false, 400, 14, 4f);
                default: return new FractureProfile(112, false, 1500, 20, 5f);
            }
        }
    }

    /// <summary>
    /// Every option from the Settings menu, persisted in PlayerPrefs and applied immediately.
    /// </summary>
    public sealed class GameSettings
    {
        public static readonly Vector2Int[] Resolutions = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };
        public const string SystemDefaultDevice = "System Default";

        // Graphics
        public int ResolutionIndex;
        public DisplayModeOption DisplayMode = DisplayModeOption.BorderlessWindow;
        public FractureQuality FractureQuality = FractureQuality.Medium;
        public bool VSync = true;
        public float FieldOfView = 90f;

        // Audio
        public float MasterVolume = 0.8f;
        public float SfxVolume = 1f;
        public float MusicVolume = 0.5f;
        public bool VoiceEnabled = true;
        public string VoiceInputDevice = SystemDefaultDevice;
        public string VoiceOutputDevice = SystemDefaultDevice;
        public bool PushToTalk = true;
        public float VoiceVolume = 1f;

        // Controls
        public float MouseSensitivity = 2f;
        public float GamepadLookSpeed = 180f;
        public bool InvertY;
        public readonly KeyCode[] Keyboard = new KeyCode[Enum.GetValues(typeof(GameAction)).Length];
        public readonly KeyCode[] Gamepad = new KeyCode[Enum.GetValues(typeof(GameAction)).Length];

        // Online
        public string ServerHosts = string.Empty;

        public GameSettings()
        {
            ResetBindings();
        }

        public void ResetBindings()
        {
            Keyboard[(int)GameAction.MoveForward] = KeyCode.W;
            Keyboard[(int)GameAction.MoveBack] = KeyCode.S;
            Keyboard[(int)GameAction.MoveLeft] = KeyCode.A;
            Keyboard[(int)GameAction.MoveRight] = KeyCode.D;
            Keyboard[(int)GameAction.Jump] = KeyCode.Space;
            Keyboard[(int)GameAction.Fire] = KeyCode.Mouse0;
            Keyboard[(int)GameAction.Anchor] = KeyCode.Mouse1;
            Keyboard[(int)GameAction.NextWeapon] = KeyCode.E;
            Keyboard[(int)GameAction.PrevWeapon] = KeyCode.Q;
            Keyboard[(int)GameAction.PushToTalk] = KeyCode.V;
            Keyboard[(int)GameAction.MuteMic] = KeyCode.M;
            Keyboard[(int)GameAction.Pause] = KeyCode.Escape;

            // Xbox layout on Windows. KeyCode.None on Fire/Anchor/Mute = use RT / LT / D-Pad Down axes.
            Gamepad[(int)GameAction.MoveForward] = KeyCode.None;
            Gamepad[(int)GameAction.MoveBack] = KeyCode.None;
            Gamepad[(int)GameAction.MoveLeft] = KeyCode.None;
            Gamepad[(int)GameAction.MoveRight] = KeyCode.None;
            Gamepad[(int)GameAction.Jump] = KeyCode.JoystickButton0;       // A / Cross
            Gamepad[(int)GameAction.Fire] = KeyCode.None;                  // RT / R2
            Gamepad[(int)GameAction.Anchor] = KeyCode.None;                // LT / L2
            Gamepad[(int)GameAction.NextWeapon] = KeyCode.JoystickButton5; // RB / R1
            Gamepad[(int)GameAction.PrevWeapon] = KeyCode.JoystickButton4; // LB / L1
            Gamepad[(int)GameAction.PushToTalk] = KeyCode.None;
            Gamepad[(int)GameAction.MuteMic] = KeyCode.None;               // D-Pad Down
            Gamepad[(int)GameAction.Pause] = KeyCode.JoystickButton7;      // START / OPTIONS
        }

        public Vector2Int Resolution => Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];

        public FullScreenMode FullScreenMode
        {
            get
            {
                switch (DisplayMode)
                {
                    case DisplayModeOption.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                    case DisplayModeOption.Windowed: return FullScreenMode.Windowed;
                    default: return FullScreenMode.FullScreenWindow;
                }
            }
        }

        public FractureProfile Fracture => FractureProfile.For(FractureQuality);

        /// <summary>Linear gain for sound effects (master × sfx), used by every SFX AudioSource.</summary>
        public float SfxGain => MasterVolume * SfxVolume;
        public float MusicGain => MasterVolume * MusicVolume;
        public float VoiceGain => MasterVolume * VoiceVolume;

        public string[] ServerHostList => ServerHosts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // ───────────────────────────── persistence ─────────────────────────────

        private const string Prefix = "gc.";

        public static GameSettings Load()
        {
            var s = new GameSettings();
            if (!PlayerPrefs.HasKey(Prefix + "version"))
            {
                s.ResolutionIndex = BestResolutionForScreen();
                return s;
            }
            s.ResolutionIndex = PlayerPrefs.GetInt(Prefix + "res", 0);
            s.DisplayMode = (DisplayModeOption)PlayerPrefs.GetInt(Prefix + "display", (int)s.DisplayMode);
            s.FractureQuality = (FractureQuality)PlayerPrefs.GetInt(Prefix + "fracture", (int)s.FractureQuality);
            s.VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            s.FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", s.FieldOfView);
            s.MasterVolume = PlayerPrefs.GetFloat(Prefix + "vol.master", s.MasterVolume);
            s.SfxVolume = PlayerPrefs.GetFloat(Prefix + "vol.sfx", s.SfxVolume);
            s.MusicVolume = PlayerPrefs.GetFloat(Prefix + "vol.music", s.MusicVolume);
            s.VoiceEnabled = PlayerPrefs.GetInt(Prefix + "voip", 1) == 1;
            s.VoiceInputDevice = PlayerPrefs.GetString(Prefix + "voip.in", s.VoiceInputDevice);
            s.VoiceOutputDevice = PlayerPrefs.GetString(Prefix + "voip.out", s.VoiceOutputDevice);
            s.PushToTalk = PlayerPrefs.GetInt(Prefix + "voip.ptt", 1) == 1;
            s.VoiceVolume = PlayerPrefs.GetFloat(Prefix + "vol.voice", s.VoiceVolume);
            s.MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "sens.mouse", s.MouseSensitivity);
            s.GamepadLookSpeed = PlayerPrefs.GetFloat(Prefix + "sens.pad", s.GamepadLookSpeed);
            s.InvertY = PlayerPrefs.GetInt(Prefix + "invert", 0) == 1;
            s.ServerHosts = PlayerPrefs.GetString(Prefix + "hosts", string.Empty);
            for (int i = 0; i < s.Keyboard.Length; i++)
            {
                s.Keyboard[i] = (KeyCode)PlayerPrefs.GetInt(Prefix + "key." + (GameAction)i, (int)s.Keyboard[i]);
                s.Gamepad[i] = (KeyCode)PlayerPrefs.GetInt(Prefix + "pad." + (GameAction)i, (int)s.Gamepad[i]);
            }
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetInt(Prefix + "version", 1);
            PlayerPrefs.SetInt(Prefix + "res", ResolutionIndex);
            PlayerPrefs.SetInt(Prefix + "display", (int)DisplayMode);
            PlayerPrefs.SetInt(Prefix + "fracture", (int)FractureQuality);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);
            PlayerPrefs.SetFloat(Prefix + "vol.master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "vol.sfx", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "vol.music", MusicVolume);
            PlayerPrefs.SetInt(Prefix + "voip", VoiceEnabled ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "voip.in", VoiceInputDevice ?? SystemDefaultDevice);
            PlayerPrefs.SetString(Prefix + "voip.out", VoiceOutputDevice ?? SystemDefaultDevice);
            PlayerPrefs.SetInt(Prefix + "voip.ptt", PushToTalk ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "vol.voice", VoiceVolume);
            PlayerPrefs.SetFloat(Prefix + "sens.mouse", MouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "sens.pad", GamepadLookSpeed);
            PlayerPrefs.SetInt(Prefix + "invert", InvertY ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "hosts", ServerHosts ?? string.Empty);
            for (int i = 0; i < Keyboard.Length; i++)
            {
                PlayerPrefs.SetInt(Prefix + "key." + (GameAction)i, (int)Keyboard[i]);
                PlayerPrefs.SetInt(Prefix + "pad." + (GameAction)i, (int)Gamepad[i]);
            }
            PlayerPrefs.Save();
        }

        public void ApplyDisplay()
        {
            Vector2Int r = Resolution;
            if (DisplayMode == DisplayModeOption.Windowed)
            {
                // Never open a window larger than the desktop.
                int maxW = Display.main.systemWidth, maxH = Display.main.systemHeight;
                if (r.x > maxW || r.y > maxH) r = new Vector2Int(Mathf.Min(r.x, maxW), Mathf.Min(r.y, maxH));
            }
            Screen.SetResolution(r.x, r.y, FullScreenMode);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 240;
        }

        private static int BestResolutionForScreen()
        {
            int h = Display.main.systemHeight;
            int best = 0;
            for (int i = 0; i < Resolutions.Length; i++) if (Resolutions[i].y <= h) best = i;
            return best;
        }

        public static string ActionLabel(GameAction a)
        {
            switch (a)
            {
                case GameAction.MoveForward: return "Move Forward";
                case GameAction.MoveBack: return "Move Back";
                case GameAction.MoveLeft: return "Move Left";
                case GameAction.MoveRight: return "Move Right";
                case GameAction.Jump: return "Jump";
                case GameAction.Fire: return "Fire Weapon";
                case GameAction.Anchor: return "Suction-Boot Anchor";
                case GameAction.NextWeapon: return "Next Weapon";
                case GameAction.PrevWeapon: return "Previous Weapon";
                case GameAction.PushToTalk: return "Push-To-Talk";
                case GameAction.MuteMic: return "Mute Microphone";
                case GameAction.Pause: return "Pause Menu";
                default: return a.ToString();
            }
        }

        public static string KeyLabel(KeyCode k, GameAction a, bool gamepad)
        {
            if (k == KeyCode.None)
            {
                if (!gamepad) return "—";
                switch (a)
                {
                    case GameAction.MoveForward:
                    case GameAction.MoveBack:
                    case GameAction.MoveLeft:
                    case GameAction.MoveRight: return "Left Stick";
                    case GameAction.Fire: return "RT / R2";
                    case GameAction.Anchor: return "LT / L2";
                    case GameAction.MuteMic: return "D-Pad Down";
                    default: return "—";
                }
            }
            switch (k)
            {
                case KeyCode.Mouse0: return "Left Click";
                case KeyCode.Mouse1: return "Right Click";
                case KeyCode.Mouse2: return "Middle Click";
                case KeyCode.JoystickButton0: return "A / Cross";
                case KeyCode.JoystickButton1: return "B / Circle";
                case KeyCode.JoystickButton2: return "X / Square";
                case KeyCode.JoystickButton3: return "Y / Triangle";
                case KeyCode.JoystickButton4: return "LB / L1";
                case KeyCode.JoystickButton5: return "RB / R1";
                case KeyCode.JoystickButton6: return "Back / Share";
                case KeyCode.JoystickButton7: return "Start / Options";
                case KeyCode.JoystickButton8: return "L-Stick Click";
                case KeyCode.JoystickButton9: return "R-Stick Click";
                default: return k.ToString();
            }
        }

        public static float Clamp01(float v) => GcMath.Clamp01(v);
    }
}
