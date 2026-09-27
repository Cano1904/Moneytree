using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// SCHNITTSTELLE (wird vom UI-Strang vollständig implementiert).
    /// Zeichnet alle Menüs, das HUD und Overlays per IMGUI.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot I { get; private set; }
        void Awake() { I = this; }
    }
}
