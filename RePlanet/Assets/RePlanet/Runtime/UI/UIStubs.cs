using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public partial class UIRoot
    {
        GameAction? capturing;
        bool photoPanel = true;
        string coopError;
        string menuTab = "inventory";
        int settingsTab;
        static readonly string[] SettingsTabs = { "Grafik", "Audio", "Steuerung", "Barrierefreiheit", "Sonstiges" };
        float mapZoom = 1f;
        float buildScroll;
        void DrawSettings(GameApp app) { }
        void DrawCoop(GameApp app) { }
        void DrawGameMenu(GameApp app) { }
        void DrawMapScreen(GameApp app) { }
        void DrawPhoto(GameApp app) { }
        void DrawTravelScreen(GameApp app) { }
        void DrawHud(GameApp app) { }
        void DrawBuild(GameApp app) { }
        void DrawToasts(GameApp app) { }
        void UpdateMapBuild(GameApp app) { }
        void OnMenuOpened() { }
        void SetMenuTab(string t) { menuTab = t; }
        void CycleMenuTab(int d) { }
        void ZoomMap(int d) { }
        void UpdateCapture(GameApp app) { capturing = null; }
    }
}
