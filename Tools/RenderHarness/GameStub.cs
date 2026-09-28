using System;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public enum AppMode { Menu, Loading, Game, PlanetSelect, Ending }
    public enum ToastKind { Info, Error }
    public static class Hud { public static void Show(string s, ToastKind k, float t) { Console.WriteLine("HUD: " + s); } }
    public static class PhotoMode { public static bool Active, ShowBefore; }
    public class Settings { public int Quality = 3, Shadows = 2; public float ViewDistance = 1f; }
    public class GameApp : MonoBehaviour
    {
        public static GameApp I;
        public WorldState W;
        public bool InGame;
        public AppMode Mode;
        public Settings Settings = new Settings();
        public event Action<string> OnPlanetChanged;
        public event Action OnSessionStarted, OnSessionEnded;
        public event Action<JObj> OnFx;
    }
}

public static class Harness
{
    public static void OnAdd(UnityEngine.Component c)
    {
        if (c is UnityEngine.ParticleSystem) c.gameObject.AddComponent<UnityEngine.ParticleSystemRenderer>();
    }
}

namespace RePlanet
{
    /// <summary>Ersatz für TerrainLook (eigene Shader) – die Prüfumgebung zählt nur Geometrie.</summary>
    public static class TerrainLook
    {
        public static UnityEngine.Material CreateTerrain(string planet, UnityEngine.Texture2D tex) { return null; }
        public static UnityEngine.Material CreateWater(string planet, System.Func<UnityEngine.Texture2D> fallbackNormals) { return null; }
        public static void SetWaterClarity(UnityEngine.Material m, string planet, float q) { }
        public static void AnimateWater(UnityEngine.Material m, float t) { }
    }
}
