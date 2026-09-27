using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public partial class UIRoot
    {
        string menuTab = "inventory";
        void DrawGameMenu(GameApp app) { }
        void DrawTravelScreen(GameApp app) { }
        void OnMenuOpened() { }
        void SetMenuTab(string t) { menuTab = t; }
        void CycleMenuTab(int d) { }
    }
}
