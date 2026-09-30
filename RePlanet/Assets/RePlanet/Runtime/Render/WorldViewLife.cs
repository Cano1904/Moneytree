using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Schmale Zugänge der „belebten Welt“ (Tiere, Stadtleben, Wind) auf Teile von WorldView – nur lesend bzw. für
    /// reine Darstellungswerte (Anteil beleuchteter Fenster, Brunnenstärke, Wiegen der Pflanzungen).
    /// </summary>
    public partial class WorldView
    {
        /// <summary>Intakte Dachhöhe eines Flachdach-Gebäudes bei x (NaN: kein begehbares Dach).</summary>
        public float RoofAt(Box bx, float x) { return RoofHeight(bx, x); }

        /// <summary>Leuchtende Straßenlaternen je Bereich (Weltpositionen der Leuchten).</summary>
        public List<Vector3> LampPositions(int area) { return area >= 0 && area < 3 ? lampPositions[area] : null; }

        /// <summary>Anteil beleuchteter Fenster je Bereich (nur mit dem eigenen Fenster-Shader wirksam).</summary>
        public void SetWindowShare(int area, float share)
        {
            Material m;
            if (windowMats.TryGetValue(area, out m) && m != null && m.HasProperty("_LitShare")) m.SetFloat("_LitShare", Mathf.Clamp01(share));
        }

        /// <summary>Brunnen: Stärke 0..1 (Rate und Höhe des Wasserstrahls), nur solange sie ohnehin laufen.</summary>
        public void SetFountainStrength(float strength)
        {
            strength = Mathf.Clamp01(strength);
            foreach (var f in fountains)
            {
                if (f == null) continue;
                var em = f.emission;
                em.rateOverTime = Mathf.Lerp(50f, 170f, strength);
                var main = f.main;
                main.startSpeed = Mathf.Lerp(4.2f, 7.2f, strength);
            }
        }

        /// <summary>Positionen der Brunnen (Wasserstrahl-Austritt) und ob sie gerade laufen.</summary>
        public void CollectFountains(List<Vector4> result)
        {
            result.Clear();
            foreach (var f in fountains)
            {
                if (f == null) continue;
                var p = f.transform.position;
                result.Add(new Vector4(p.x, p.y, p.z, f.emission.enabled ? 1f : 0f));
            }
        }

        /// <summary>Gepflanzte Ökologie-Pflanzen (Wurzel am Boden) – zum Wiegen im Wind.</summary>
        public Dictionary<string, Transform> EcoPlants { get { return ecoVisuals; } }
    }
}
