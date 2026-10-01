// Erweiterung des UnityEngine-Ersatzes für Intro, Abspann, Atmosphäre, Figuren, Effekte und Spielablauf.
// Alles ohne Wirkung, aber mit denselben Grenzen/Prüfungen wie Unity, wo ein Fehler sonst unbemerkt bliebe
// (z. B. Indizes in SetParticles, NaN in Transformen – siehe NanGuard).
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    using UnityEngine.Rendering;

    public class DefaultExecutionOrderAttribute : Attribute { public readonly int order; public DefaultExecutionOrderAttribute(int o) { order = o; } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public readonly RuntimeInitializeLoadType loadType = RuntimeInitializeLoadType.AfterSceneLoad; public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { loadType = t; } public RuntimeInitializeOnLoadMethodAttribute() { } }
    public class SerializeField : Attribute { }
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public class ExecuteAlways : Attribute { }
    public class ImageEffectAllowedInSceneView : Attribute { }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public override string ToString() => $"({x}, {y})";
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public Vector2 min => new Vector2(x, y);
        public Vector2 max => new Vector2(x + width, y + height);
        public float xMin => x; public float yMin => y; public float xMax => x + width; public float yMax => y + height;
        public Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);
        public bool Contains(Vector2 p) => p.x >= x && p.y >= y && p.x < x + width && p.y < y + height;
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum LightRenderMode { Auto, ForcePixel, ForceVertex }
    public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public enum RenderTextureFormat { ARGB32, Depth, ARGBHalf, Shadowmap, RGB565, ARGB4444, ARGB1555, Default, ARGB2101010, DefaultHDR, RGB111110Float = 22, RFloat = 14, RHalf = 15 }
    public enum RenderTextureReadWrite { Default, Linear, sRGB }
    public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }
    public enum EventType { MouseDown, MouseUp, MouseMove, MouseDrag, KeyDown, KeyUp, ScrollWheel, Repaint, Layout }
    public enum FullScreenMode { ExclusiveFullScreen, FullScreenWindow, MaximizedWindow, Windowed }
    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public enum ShadowProjection { CloseFit, StableFit }
    public enum AudioReverbPreset { Off, Generic, PaddedCell, Room, Bathroom, Livingroom, Stoneroom, Auditorium, Concerthall, Cave, Arena, Hangar, User = 27 }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public enum LineAlignment { View, TransformZ }
    public enum DepthTextureMode { None = 0, Depth = 1, DepthNormals = 2, MotionVectors = 4 }

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        Alpha0 = 48, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        A = 97, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Delete = 127, UpArrow = 273, DownArrow, RightArrow, LeftArrow, Insert, Home, End, PageUp, PageDown,
        F1 = 282, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        RightShift = 303, LeftShift, RightControl, LeftControl, RightAlt, LeftAlt,
        Mouse0 = 323, Mouse1, Mouse2, Mouse3, Mouse4,
        JoystickButton0 = 330, JoystickButton1, JoystickButton2, JoystickButton3, JoystickButton4, JoystickButton5,
        JoystickButton6, JoystickButton7, JoystickButton8, JoystickButton9, JoystickButton10, JoystickButton11,
        JoystickButton12, JoystickButton13, JoystickButton14, JoystickButton15, JoystickButton16, JoystickButton17,
        JoystickButton18, JoystickButton19,
        Minus = 45, Plus = 43, Equals = 61, Comma = 44, Period = 46, KeypadPlus = 270, KeypadMinus = 269, KeypadEnter = 271,
        CapsLock = 301, Backslash = 92, Slash = 47, Semicolon = 59, Quote = 39, LeftBracket = 91, RightBracket = 93, BackQuote = 96,
    }

    /// <summary>Eingabe: die Prüfumgebung kann Tasten und Achsen vorgeben.</summary>
    public static class Input
    {
        public static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>();
        public static readonly Dictionary<string, float> Axes = new Dictionary<string, float>();
        public static bool GetKey(KeyCode k) => Held.Contains(k);
        public static bool GetKeyDown(KeyCode k) => Down.Contains(k);
        public static bool GetKeyUp(KeyCode k) => false;
        public static bool GetMouseButton(int b) => Held.Contains(KeyCode.Mouse0 + b);
        public static bool GetMouseButtonDown(int b) => Down.Contains(KeyCode.Mouse0 + b);
        public static bool GetMouseButtonUp(int b) => false;
        public static float GetAxis(string a) => Axes.TryGetValue(a, out var v) ? v : 0f;
        public static float GetAxisRaw(string a) => GetAxis(a);
        public static bool anyKeyDown => Down.Count > 0;
        public static bool anyKey => Held.Count > 0;
        public static Vector3 mousePosition = new Vector3(480, 270, 0);
        public static Vector2 mouseScrollDelta => Vector2.zero;
        public static string[] GetJoystickNames() => new string[0];
        public static string inputString => "";
    }

    public struct Resolution { public int width, height; public double refreshRateRatio; public int refreshRate; }
    public static class Screen
    {
        public static int width = 1920, height = 1080;
        public static float dpi = 96;
        public static Resolution currentResolution => new Resolution { width = 1920, height = 1080, refreshRate = 60 };
        public static Resolution[] resolutions => new[] { currentResolution };
        public static FullScreenMode fullScreenMode;
        public static bool fullScreen;
        public static void SetResolution(int w, int h, FullScreenMode m) { }
        public static void SetResolution(int w, int h, bool f) { }
    }

    public static class Application
    {
        public static bool runInBackground, isEditor = false, isFocused = true;
        public static int targetFrameRate = -1;
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "replanet-harness");
        public static string dataPath = persistentDataPath;
        public static string version = "harness";
        public static RuntimePlatform platform => RuntimePlatform.LinuxPlayer;
        public static void Quit() { }
        public static void OpenURL(string u) { }
        public static SystemLanguage systemLanguage => SystemLanguage.German;
    }
    public enum RuntimePlatform { LinuxPlayer, WindowsPlayer, OSXPlayer }
    public enum SystemLanguage { German, English, French, Spanish, Italian, Unknown }

    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public static class QualitySettings
    {
        public static ColorSpace activeColorSpace = ColorSpace.Linear;
        public static int antiAliasing = 4, vSyncCount = 1, shadowCascades = 2, pixelLightCount = 4;
        public static float lodBias = 1f, shadowDistance = 80f, shadowCascade2Split = 0.33f;
        public static Vector3 shadowCascade4Split;
        public static string[] names = { "Niedrig", "Mittel", "Hoch", "Ultra" };
        public static bool realtimeReflectionProbes, softParticles;
        public static ShadowQuality shadows;
        public static ShadowResolution shadowResolution;
        public static ShadowProjection shadowProjection;
        public static AnisotropicFiltering anisotropicFiltering;
        public static void SetQualityLevel(int i, bool b) { }
        public static void SetQualityLevel(int i) { }
        public static int GetQualityLevel() => 2;
    }
    public enum AnisotropicFiltering { Disable, Enable, ForceEnable }

    public static class RenderSettings
    {
        public static bool fog;
        public static FogMode fogMode = FogMode.Exponential;
        public static float fogDensity, fogStartDistance, fogEndDistance, ambientIntensity = 1f, reflectionIntensity = 1f;
        public static Color fogColor, ambientSkyColor, ambientEquatorColor, ambientGroundColor, ambientLight, subtractiveShadowColor;
        public static AmbientMode ambientMode;
        public static Material skybox;
        public static Light sun;
        public static DefaultReflectionMode defaultReflectionMode;
        public static int defaultReflectionResolution;
        public static Texture customReflection;
    }

    public static class AudioSettings { public static double dspTime => Time.time; public static int outputSampleRate => 48000; }

    public class Light : Behaviour
    {
        public LightType type = LightType.Point;
        public Color color = Color.white;
        public float intensity = 1f, range = 10f, spotAngle = 30f, shadowStrength = 1f, shadowBias, shadowNormalBias, bounceIntensity = 1f, shadowNearPlane = 0.2f, innerSpotAngle;
        public LightShadows shadows;
        public LightRenderMode renderMode;
        public HideFlags hideFlags;
        public int cullingMask = -1;
        public Texture cookie;
        public float cookieSize = 10f;
        public LightShadowResolution shadowResolution;
        public float colorTemperature = 6570f; public bool useColorTemperature;
        public LightmapBakeType lightmapBakeType;
    }
    public enum LightmapBakeType { Realtime = 4, Baked = 2, Mixed = 1 }

    public class ReflectionProbe : Behaviour
    {
        public ReflectionProbeMode mode; public ReflectionProbeRefreshMode refreshMode; public ReflectionProbeTimeSlicingMode timeSlicingMode;
        public Vector3 size, center; public int resolution; public float intensity = 1f, farClipPlane = 1000f, nearClipPlane = 0.3f; public bool boxProjection, hdr;
        public ReflectionProbeClearFlags clearFlags; public Color backgroundColor; public int cullingMask = -1; public float blendDistance; public int importance = 1;
        public static int Renders;
        public int RenderProbe() { Renders++; return 1; }
        public bool IsFinishedRendering(int id) => true;
        public Texture texture => null;
    }

    public class AudioClip : Object
    {
        public float length = 3f; public int samples = 144000, frequency = 48000, channels = 1;
        public static AudioClip Create(string n, int len, int ch, int freq, bool stream) => new AudioClip { name = n, samples = len, channels = ch, frequency = freq, length = len / (float)freq };
        public bool SetData(float[] d, int off) => true;
        public bool GetData(float[] d, int off) => true;
        public AudioDataLoadState loadState => AudioDataLoadState.Loaded;
        public bool LoadAudioData() => true;
    }
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }

    public class AudioSource : Behaviour
    {
        public AudioClip clip; public float volume = 1f, pitch = 1f, spatialBlend, time, minDistance = 1f, maxDistance = 500f, dopplerLevel = 1f, spread, reverbZoneMix = 1f, panStereo;
        public bool loop, playOnAwake, isPlaying, mute, bypassEffects, bypassReverbZones, ignoreListenerPause, ignoreListenerVolume;
        public int priority = 128, timeSamples;
        public AudioRolloffMode rolloffMode;
        public void Play() { isPlaying = clip != null; }
        public void PlayScheduled(double t) { isPlaying = clip != null; }
        public void SetScheduledStartTime(double t) { }
        public void SetScheduledEndTime(double t) { }
        public void Stop() { isPlaying = false; }
        public void Pause() { isPlaying = false; }
        public void UnPause() { }
        public void PlayOneShot(AudioClip c, float v = 1f) { }
    }
    public class AudioReverbFilter : Behaviour
    {
        public AudioReverbPreset reverbPreset;
        public float dryLevel, room, roomHF, roomLF, decayTime = 1f, decayHFRatio = 0.5f, reflectionsLevel, reflectionsDelay, reverbLevel, reverbDelay, hfReference = 5000f, lfReference = 250f, diffusion = 100f, density = 100f;
    }
    public class AudioLowPassFilter : Behaviour { public float cutoffFrequency = 5000f, lowpassResonanceQ = 1f; }
    public class AudioHighPassFilter : Behaviour { public float cutoffFrequency = 5000f, highpassResonanceQ = 1f; }
    public class AudioListener : Behaviour { public static float volume = 1f; public static bool pause; }
    public class FlareLayer : Behaviour { }

    public class LineRenderer : Renderer
    {
        public bool useWorldSpace = true, loop;
        public float startWidth = 1f, endWidth = 1f, widthMultiplier = 1f;
        public Color startColor = Color.white, endColor = Color.white;
        public LineAlignment alignment;
        public LineTextureMode textureMode;
        public int numCapVertices, numCornerVertices;
        Vector3[] pts = new Vector3[0];
        public int positionCount { get => pts.Length; set { Array.Resize(ref pts, Math.Max(0, value)); } }
        public void SetPosition(int i, Vector3 p) { if (i < 0 || i >= pts.Length) throw new IndexOutOfRangeException("LineRenderer.SetPosition " + i + " / " + pts.Length); NanGuard.Check(p, "LineRenderer.SetPosition"); pts[i] = p; }
        public void SetPositions(Vector3[] p) { for (int i = 0; i < Math.Min(p.Length, pts.Length); i++) SetPosition(i, p[i]); }
        public Vector3 GetPosition(int i) => pts[i];
    }
    public enum LineTextureMode { Stretch, Tile }

    public class TrailRenderer : Renderer { public float time = 1f, startWidth, endWidth, minVertexDistance = 0.1f; public bool emitting = true; public void Clear() { } }

    public class Texture2DArray : Texture { }
    public class Cubemap : Texture { }

    public class RenderTexture : Texture
    {
        public int width, height, depth, antiAliasing = 1; public RenderTextureFormat format;
        public static RenderTexture active;
        public RenderTexture(int w, int h, int d, RenderTextureFormat f = RenderTextureFormat.Default) { width = w; height = h; depth = d; format = f; }
        public RenderTexture(int w, int h, int d, RenderTextureFormat f, RenderTextureReadWrite rw) : this(w, h, d, f) { }
        public bool Create() => true;
        public void Release() { }
        public bool IsCreated() => true;
        public static RenderTexture GetTemporary(int w, int h, int d = 0, RenderTextureFormat f = RenderTextureFormat.Default, RenderTextureReadWrite rw = RenderTextureReadWrite.Default, int aa = 1) => new RenderTexture(w, h, d, f);
        public static void ReleaseTemporary(RenderTexture t) { }
    }

    public struct Keyframe { public float time, value, inTangent, outTangent; public Keyframe(float t, float v) { time = t; value = v; inTangent = outTangent = 0; } public Keyframe(float t, float v, float i, float o) { time = t; value = v; inTangent = i; outTangent = o; } }
    public class AnimationCurve
    {
        public Keyframe[] keys;
        public AnimationCurve(params Keyframe[] k) { keys = k; }
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1) => new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1));
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1) => Linear(t0, v0, t1, v1);
        public static AnimationCurve Constant(float t0, float t1, float v) => Linear(t0, v, t1, v);
        public int AddKey(float t, float v) { var l = new List<Keyframe>(keys) { new Keyframe(t, v) }; keys = l.ToArray(); return keys.Length - 1; }
        public float Evaluate(float t) { if (keys.Length == 0) return 0; return keys[0].value; }
        public int length => keys.Length;
    }
    public struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color c, float t) { color = c; time = t; } }
    public struct GradientAlphaKey { public float alpha, time; public GradientAlphaKey(float a, float t) { alpha = a; time = t; } }
    public class Gradient
    {
        public GradientColorKey[] colorKeys = new GradientColorKey[0]; public GradientAlphaKey[] alphaKeys = new GradientAlphaKey[0];
        public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a)
        {
            if (c == null || a == null) throw new ArgumentNullException();
            if (c.Length > 8 || a.Length > 8) Debug.LogWarning("Gradient: mehr als 8 Schlüssel (Unity kappt)");
            colorKeys = c; alphaKeys = a;
        }
        public Color Evaluate(float t) => colorKeys.Length > 0 ? colorKeys[0].color : Color.white;
    }

    // ------------------------------------------------------------------ Partikel
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Mesh = 6, Circle = 10, Edge = 12, Rectangle = 18, Donut = 17 }
    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemCullingMode { Automatic, PauseAndCatchup, Pause, AlwaysSimulate }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public enum ParticleSystemCurveMode { Constant, Curve, TwoCurves, TwoConstants }
    public enum ParticleSystemGradientMode { Color, Gradient, TwoColors, TwoGradients, RandomColor }
    public enum ParticleSystemRenderSpace { View, World, Local, Facing, Velocity }
    public enum ParticleSystemSortMode { None, Distance, OldestInFront, YoungestInFront }

    public class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public float constant, constantMin, constantMax, curveMultiplier; public AnimationCurve curve; public ParticleSystemCurveMode mode;
            public MinMaxCurve(float c) { constant = constantMin = constantMax = c; curveMultiplier = 1; curve = null; mode = ParticleSystemCurveMode.Constant; }
            public MinMaxCurve(float a, float b) { constant = b; constantMin = a; constantMax = b; curveMultiplier = 1; curve = null; mode = ParticleSystemCurveMode.TwoConstants; }
            public MinMaxCurve(float m, AnimationCurve c) { constant = m; constantMin = constantMax = m; curveMultiplier = m; curve = c; mode = ParticleSystemCurveMode.Curve; }
            public MinMaxCurve(float m, AnimationCurve a, AnimationCurve b) : this(m, a) { mode = ParticleSystemCurveMode.TwoCurves; }
            public static implicit operator MinMaxCurve(float f) => new MinMaxCurve(f);
            public float Evaluate(float t) => constant;
        }
        public struct MinMaxGradient
        {
            public Color color, colorMin, colorMax; public Gradient gradient; public ParticleSystemGradientMode mode;
            public MinMaxGradient(Color c) { color = colorMin = colorMax = c; gradient = null; mode = ParticleSystemGradientMode.Color; }
            public MinMaxGradient(Color a, Color b) { color = b; colorMin = a; colorMax = b; gradient = null; mode = ParticleSystemGradientMode.TwoColors; }
            public MinMaxGradient(Gradient g) { color = colorMin = colorMax = Color.white; gradient = g; mode = ParticleSystemGradientMode.Gradient; }
            public MinMaxGradient(Gradient a, Gradient b) : this(a) { mode = ParticleSystemGradientMode.TwoGradients; }
            public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
            public static implicit operator MinMaxGradient(Gradient g) => new MinMaxGradient(g);
        }
        public class MainModule
        {
            public MinMaxCurve startLifetime = 5f, startSpeed = 5f, startSize = 1f, startRotation, gravityModifier, startDelay, startSizeX, startSizeY, startSizeZ, startRotationX, startRotationY, startRotationZ;
            public MinMaxGradient startColor = Color.white;
            public float duration = 5f, simulationSpeed = 1f, gravityModifierMultiplier = 1f, startLifetimeMultiplier = 1f, startSpeedMultiplier = 1f, startSizeMultiplier = 1f, flipRotation;
            public int maxParticles = 1000;
            public bool loop = true, playOnAwake = true, prewarm, startSize3D, startRotation3D, useUnscaledTime;
            public ParticleSystemSimulationSpace simulationSpace; public Transform customSimulationSpace;
            public ParticleSystemCullingMode cullingMode; public ParticleSystemScalingMode scalingMode;
            public ParticleSystemStopAction stopAction;
            public ParticleSystemEmitterVelocityMode emitterVelocityMode;
        }
        public class ShapeModule
        {
            public bool enabled = true, alignToDirection;
            public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
            public float angle = 25f, radius = 1f, radiusThickness = 1f, arc = 360f, length = 5f, randomDirectionAmount, sphericalDirectionAmount, randomPositionAmount, donutRadius;
            public Vector3 scale = Vector3.one, position, rotation, boxThickness;
            public Mesh mesh; public MeshRenderer meshRenderer;
        }
        public class EmissionModule
        {
            public bool enabled = true;
            public MinMaxCurve rateOverTime = 10f, rateOverDistance;
            public float rateOverTimeMultiplier = 1f, rateOverDistanceMultiplier = 1f;
            Burst[] bursts = new Burst[0];
            public int burstCount { get => bursts.Length; set => Array.Resize(ref bursts, value); }
            public void SetBursts(Burst[] b) { bursts = (Burst[])b.Clone(); }
            public void SetBursts(Burst[] b, int n) { if (n > b.Length) throw new IndexOutOfRangeException("SetBursts"); bursts = new Burst[n]; Array.Copy(b, bursts, n); }
            public void SetBurst(int i, Burst b) { bursts[i] = b; }
            public Burst GetBurst(int i) => bursts[i];
        }
        public struct Burst
        {
            public float time, probability, repeatInterval; public MinMaxCurve count; public int cycleCount; public short minCount, maxCount;
            public Burst(float t, short c) { time = t; count = c; minCount = maxCount = c; probability = 1; repeatInterval = 0.01f; cycleCount = 1; }
            public Burst(float t, short a, short b) { time = t; count = new MinMaxCurve(a, b); minCount = a; maxCount = b; probability = 1; repeatInterval = 0.01f; cycleCount = 1; }
            public Burst(float t, MinMaxCurve c) { time = t; count = c; minCount = maxCount = (short)c.constant; probability = 1; repeatInterval = 0.01f; cycleCount = 1; }
            public Burst(float t, float c) : this(t, new MinMaxCurve(c)) { }
        }
        public class ColorOverLifetimeModule { public bool enabled; public MinMaxGradient color; }
        public class SizeOverLifetimeModule { public bool enabled, separateAxes; public MinMaxCurve size, x, y, z; public float sizeMultiplier = 1f; }
        public class VelocityOverLifetimeModule { public bool enabled; public MinMaxCurve x, y, z, orbitalX, orbitalY, orbitalZ, radial, speedModifier = 1f; public ParticleSystemSimulationSpace space; public float xMultiplier = 1, yMultiplier = 1, zMultiplier = 1; }
        public class LimitVelocityOverLifetimeModule { public bool enabled, separateAxes; public MinMaxCurve limit, drag; public float dampen, limitMultiplier = 1f; public bool multiplyDragByParticleSize, multiplyDragByParticleVelocity; }
        public class NoiseModule { public bool enabled, damping = true, separateAxes; public MinMaxCurve strength = 1f, scrollSpeed; public float frequency = 0.5f, strengthMultiplier = 1f; public int octaveCount = 1; public ParticleSystemNoiseQuality quality; }
        public class RotationOverLifetimeModule { public bool enabled; public MinMaxCurve z, x, y; }
        public class ForceOverLifetimeModule { public bool enabled; public MinMaxCurve x, y, z; public ParticleSystemSimulationSpace space; }
        public class TextureSheetAnimationModule { public bool enabled; public int numTilesX = 1, numTilesY = 1; }
        public class TrailModule { public bool enabled; }
        public class CollisionModule { public bool enabled; }

        public struct Particle
        {
            public Vector3 position, velocity, rotation3D, angularVelocity3D, startSize3D;
            public float startSize, startLifetime, remainingLifetime, rotation, angularVelocity;
            public Color32 startColor; public uint randomSeed;
        }
        public struct EmitParams
        {
            public Vector3 position, velocity, rotation3D, startSize3D; public float startSize, startLifetime, rotation; public Color32 startColor;
            public bool applyShapeToPosition; public uint randomSeed; public float angularVelocity;
        }

        public MainModule main { get; } = new MainModule();
        public ShapeModule shape { get; } = new ShapeModule();
        public EmissionModule emission { get; } = new EmissionModule();
        public ColorOverLifetimeModule colorOverLifetime { get; } = new ColorOverLifetimeModule();
        public SizeOverLifetimeModule sizeOverLifetime { get; } = new SizeOverLifetimeModule();
        public VelocityOverLifetimeModule velocityOverLifetime { get; } = new VelocityOverLifetimeModule();
        public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get; } = new LimitVelocityOverLifetimeModule();
        public NoiseModule noise { get; } = new NoiseModule();
        public RotationOverLifetimeModule rotationOverLifetime { get; } = new RotationOverLifetimeModule();
        public ForceOverLifetimeModule forceOverLifetime { get; } = new ForceOverLifetimeModule();
        public TextureSheetAnimationModule textureSheetAnimation { get; } = new TextureSheetAnimationModule();
        public TrailModule trails { get; } = new TrailModule();
        public CollisionModule collision { get; } = new CollisionModule();

        public bool isPlaying, isEmitting, isStopped = true, isPaused;
        public int particleCount;
        public float time;
        public void Stop(bool children = true, ParticleSystemStopBehavior b = ParticleSystemStopBehavior.StopEmitting) { isPlaying = false; isStopped = true; if (b == ParticleSystemStopBehavior.StopEmittingAndClear) particleCount = 0; }
        public void Stop() => Stop(true);
        public void Play(bool children = true) { isPlaying = true; isStopped = false; }
        public void Play() => Play(true);
        public void Pause(bool children = true) { isPaused = true; }
        public void Clear(bool children = true) { particleCount = 0; }
        public void Clear() => Clear(true);
        public void Simulate(float t, bool c = true, bool r = true, bool f = true) { }
        public void Emit(int n) { if (n < 0) throw new ArgumentException("Emit"); particleCount = Math.Min(main.maxParticles, particleCount + n); }
        public void Emit(EmitParams p, int n) { NanGuard.Check(p.position, "ParticleSystem.Emit"); NanGuard.Check(p.velocity, "ParticleSystem.Emit.velocity"); Emit(n); }
        public void SetParticles(Particle[] ps, int n)
        {
            if (ps == null) throw new ArgumentNullException();
            if (n > ps.Length || n < 0) throw new ArgumentOutOfRangeException("SetParticles: size " + n + " > " + ps.Length);
            for (int i = 0; i < n; i++) { NanGuard.Check(ps[i].position, "SetParticles.position"); NanGuard.Check(ps[i].velocity, "SetParticles.velocity"); if (float.IsNaN(ps[i].startSize)) throw new ArithmeticException("SetParticles: startSize NaN"); }
            particleCount = Math.Min(n, main.maxParticles);
        }
        public void SetParticles(Particle[] ps) => SetParticles(ps, ps.Length);
        public int GetParticles(Particle[] ps) => 0;
        public bool IsAlive(bool c = true) => isPlaying;
    }
    public enum ParticleSystemStopAction { None, Disable, Destroy, Callback }
    public enum ParticleSystemEmitterVelocityMode { Transform, Rigidbody }
    public enum ParticleSystemNoiseQuality { Low, Medium, High }
    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode; public float maxParticleSize = 0.5f, minParticleSize, lengthScale = 2f, velocityScale, cameraVelocityScale, sortingFudge, normalDirection = 1f;
        public ParticleSystemRenderSpace alignment; public ParticleSystemSortMode sortMode; public Mesh mesh; public Vector3 pivot;
        public bool allowRoll = true;
    }

    // ------------------------------------------------------------------ Oberfläche (nur für OnGUI-Methoden, die hier nie laufen)
    public class Event { public static Event current = new Event(); public EventType type = EventType.Layout; public KeyCode keyCode; public Vector2 mousePosition; public void Use() { } }
    public class GUIStyle
    {
        public GUIStyle() { } public GUIStyle(GUIStyle o) { }
        public int fontSize; public FontStyle fontStyle; public TextAnchor alignment; public bool wordWrap, richText, clipping;
        public GUIStyleState normal = new GUIStyleState(), hover = new GUIStyleState(), active = new GUIStyleState();
        public RectOffset padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset();
        public Font font; public float fixedHeight, fixedWidth;
        public Vector2 CalcSize(GUIContent c) => new Vector2(100, 20);
        public float CalcHeight(GUIContent c, float w) => 20;
    }
    public class GUIStyleState { public Color textColor = Color.white; public Texture2D background; }
    public class RectOffset { public int left, right, top, bottom; public RectOffset() { } public RectOffset(int l, int r, int t, int b) { left = l; right = r; top = t; bottom = b; } }
    public class GUIContent { public string text; public GUIContent(string t) { text = t; } public GUIContent() { } public static GUIContent none = new GUIContent(); }
    public class GUISkin { public GUIStyle label = new GUIStyle(), box = new GUIStyle(), button = new GUIStyle(); public Font font; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public static class GUI
    {
        public static Color color = Color.white, contentColor = Color.white, backgroundColor = Color.white;
        public static int depth;
        public static GUISkin skin = new GUISkin();
        public static bool enabled = true;
        public static Matrix4x4 matrix = Matrix4x4.identity;
        public static void DrawTexture(Rect r, Texture t) { }
        public static void DrawTexture(Rect r, Texture t, ScaleMode m) { }
        public static void DrawTexture(Rect r, Texture t, ScaleMode m, bool a) { }
        public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv) { }
        public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv, bool a) { }
        public static void Label(Rect r, string s) { }
        public static void Label(Rect r, string s, GUIStyle st) { }
        public static void Label(Rect r, GUIContent s, GUIStyle st) { }
        public static void Box(Rect r, string s) { }
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public enum DefaultReflectionMode { Skybox, Custom }
    public enum ReflectionProbeMode { Baked, Realtime, Custom }
    public enum ReflectionProbeRefreshMode { OnAwake, EveryFrame, ViaScripting }
    public enum ReflectionProbeTimeSlicingMode { AllFacesAtOnce, IndividualFaces, NoTimeSlicing }
    public enum ReflectionProbeClearFlags { Skybox = 1, SolidColor = 2 }
    public enum CompareFunction { Disabled, Never, Less, Equal, LessEqual, Greater, NotEqual, GreaterEqual, Always }
    public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha }
    public enum CullMode { Off, Front, Back }
    public enum RenderQueue { Background = 1000, Geometry = 2000, AlphaTest = 2450, GeometryLast = 2500, Transparent = 3000, Overlay = 4000 }
}

namespace UnityEngine
{
    public enum LightShadowResolution { FromQualitySettings = -1, Low, Medium, High, VeryHigh }
    public enum TextAnchorStub { }

    /// <summary>Unity wirft bei NaN/∞ in Transformen nur eine Konsolenmeldung und das Objekt verschwindet – hier wird es ein Fehler.</summary>
    public static class NanGuard
    {
        public static bool Enabled = true;
        public static readonly List<string> Hits = new List<string>();
        public static void Check(Vector3 v, string where)
        {
            if (!Enabled) return;
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                throw new ArithmeticException("NaN/∞ in " + where + ": " + v.x + ", " + v.y + ", " + v.z);
        }
        public static void Check(Quaternion q, string where)
        {
            if (!Enabled) return;
            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w))
                throw new ArithmeticException("NaN in " + where + " (Rotation)");
        }
    }
}
