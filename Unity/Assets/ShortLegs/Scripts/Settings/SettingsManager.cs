using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

namespace ShortLegs.Settings
{
    public enum ArtStyle { VibrantComic = 0, ClassicNoir = 1 }
    public enum SubtitleSize { Small = 0, Medium = 1, Large = 2, ExtraLarge = 3 }

    /// <summary>Everything in the Settings menu (GDD §4). Saved as versioned JSON in the local profile.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public int Version = 1;
        public string PlayerName = "Detective";

        // 4.1 Graphics
        public int ResolutionWidth;
        public int ResolutionHeight;
        public FullScreenMode DisplayMode = FullScreenMode.FullScreenWindow; // Fullscreen / Borderless / Windowed
        public ArtStyle ArtStyle = ArtStyle.VibrantComic;
        public bool ReduceFlashing;

        // 4.1 Audio (0..1)
        public float MasterVolume = 1f;
        public float MusicVolume = 0.7f;
        public float VoiceChatVolume = 1f;
        public float SfxVolume = 0.9f;

        // 4.2 Accessibility & Language
        public bool TextToSpeech;
        public bool SpeechToText;
        public bool Subtitles = true;
        public SubtitleSize SubtitleSize = SubtitleSize.Large;
        public bool HighContrastSubtitles = true;
        public string LanguageCode = "de";

        // Controls
        public float LookSensitivity = 0.12f;
        public string BindingOverridesJson = "";

        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
    }

    public sealed class SettingsManager : MonoBehaviour
    {
        public const int MaxWidth = 3840, MaxHeight = 2160; // up to 4K

        public static SettingsManager Instance { get; private set; }

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private InputActionAsset controls;
        [Tooltip("Exposed mixer parameters.")]
        [SerializeField] private string masterParam = "MasterVolume", musicParam = "MusicVolume",
            voiceParam = "VoiceVolume", sfxParam = "SfxVolume";

        public GameSettings Current { get; private set; } = new GameSettings();
        public event Action<GameSettings> Applied;

        private GameSettings _saved;
        private static string PathOnDisk => Path.Combine(Application.persistentDataPath, "profile_settings.json");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            Apply(Current);
        }

        public void Load()
        {
            try
            {
                if (File.Exists(PathOnDisk))
                    Current = Migrate(JsonUtility.FromJson<GameSettings>(File.ReadAllText(PathOnDisk)));
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"[ShortLegs] Settings unreadable, using defaults: {e.Message}");
                Current = new GameSettings();
            }
            if (Current.ResolutionWidth <= 0)
            {
                Current.ResolutionWidth = Math.Min(Screen.currentResolution.width, MaxWidth);
                Current.ResolutionHeight = Math.Min(Screen.currentResolution.height, MaxHeight);
            }
            _saved = Current.Clone();
        }

        private static GameSettings Migrate(GameSettings s)
        {
            if (s == null) return new GameSettings();
            // Future: if (s.Version < 2) { ... }
            s.Version = 1;
            return s;
        }

        /// <summary>Live preview while the menu is open.</summary>
        public void Preview(GameSettings s)
        {
            Current = s;
            Apply(s);
        }

        /// <summary>[APPLY] — persist.</summary>
        public void Save()
        {
            if (controls != null) Current.BindingOverridesJson = controls.SaveBindingOverridesAsJson();
            File.WriteAllText(PathOnDisk, JsonUtility.ToJson(Current, true));
            _saved = Current.Clone();
        }

        /// <summary>[BACK] without applying — restore what was on disk.</summary>
        public void Revert()
        {
            Current = _saved.Clone();
            Apply(Current);
        }

        private void Apply(GameSettings s)
        {
            int w = Mathf.Clamp(s.ResolutionWidth, 640, MaxWidth);
            int h = Mathf.Clamp(s.ResolutionHeight, 360, MaxHeight);
            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != s.DisplayMode)
                Screen.SetResolution(w, h, s.DisplayMode);

            if (mixer != null)
            {
                mixer.SetFloat(masterParam, ToDecibels(s.MasterVolume));
                mixer.SetFloat(musicParam, ToDecibels(s.MusicVolume));
                mixer.SetFloat(voiceParam, ToDecibels(s.VoiceChatVolume));
                mixer.SetFloat(sfxParam, ToDecibels(s.SfxVolume));
            }

            if (controls != null && !string.IsNullOrEmpty(s.BindingOverridesJson))
                controls.LoadBindingOverridesFromJson(s.BindingOverridesJson);

            ApplyLanguage(s.LanguageCode);
            Applied?.Invoke(s);
        }

        private static void ApplyLanguage(string code)
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null && LocalizationSettings.SelectedLocale != locale) LocalizationSettings.SelectedLocale = locale;
        }

        /// <summary>Slider 0..1 → mixer dB: 20·log10(v), silent at 0.</summary>
        public static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);

        public static Resolution[] AvailableResolutions()
        {
            var all = Screen.resolutions;
            return Array.FindAll(all, r => r.width <= MaxWidth && r.height <= MaxHeight);
        }
    }
}
