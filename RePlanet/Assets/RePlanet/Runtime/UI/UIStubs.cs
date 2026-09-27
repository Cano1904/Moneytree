using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public partial class UIRoot
    {
        bool photoPanel = true;
        string menuTab = "inventory";
        float buildScroll;
        void DrawGameMenu(GameApp app) { }
        void DrawPhoto(GameApp app) { }
        void DrawTravelScreen(GameApp app) { }
        void DrawBuild(GameApp app) { }
        void OnMenuOpened() { }
        void SetMenuTab(string t) { menuTab = t; }
        void CycleMenuTab(int d) { }
    }
}
