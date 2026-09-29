using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Stadtklänge: Klangschichten, die mit der Wiederherstellung wachsen (Lautstärke = Einstellung „Umgebung“).
    /// <list type="bullet">
    /// <item>Vögel (TERRA, schwächer auf PYRA) bzw. Möwen (PELAGIA): tagsüber, mit Sauberkeit und Ökologie des Bereichs, in dem MIKO steht.</item>
    /// <item>Blätterrauschen: mit der Begrünung und dem Wind.</item>
    /// <item>Brunnen: räumliche Quellen an den Brunnen, sobald ihr Projekt fertig ist.</item>
    /// <item>Fernes Stadtleben (Verkehr, Straßenbahn, Stimmen): wenn die Stadt „erwacht“ – mit jedem fertigen Projekt des Planeten lauter.</item>
    /// </list>
    /// Sturm überdeckt die Schichten, Nacht dämpft Vögel und Stadt; im Unterschlupf leiser.
    /// </summary>
    public partial class AudioManager
    {
        readonly Dictionary<string, float> cityLevel = new Dictionary<string, float>();
        readonly List<Vector3> fountainPos = new List<Vector3>();
        readonly List<string> fountainLit = new List<string>();
        string fountainPlanet;
        int fountainLoops;

        float CitySmooth(string key, float target, float dt, float tau = 2.5f)
        {
            float v;
            if (!cityLevel.TryGetValue(key, out v)) v = 0f;
            v += (target - v) * (1f - Mathf.Exp(-dt / tau));
            cityLevel[key] = v;
            return v;
        }

        void CityOff()
        {
            foreach (var id in Synth.CityLayers) AmbLoop("amb_" + id, null, false, 0f, 0f);
            for (int i = 0; i < fountainLoops; i++) LoopInternal("amb_fountain_" + i, null, false, null, 0f, 1f, Cat.Ambient);
            cityLevel.Clear();
        }

        void UpdateCity(float dt, WorldState w, PlanetState ps, string pl, float dark, float storm, float wind, float shelter)
        {
            var me = GameApp.I != null ? GameApp.I.Me : null;
            int area = me != null ? Mathf.Clamp(PlanetLayout.AreaOf(me.Pos.z), 0, 2) : 0;
            float clean = Rules.Cleanliness(ps, area);
            float eco = Rules.EcoFraction(w, ps, area);
            ProjectState pst;
            bool projHere = ps.Projects.TryGetValue(GameData.ProjectId(pl, area), out pst) && pst.Done;
            int projects = 0;
            foreach (var kv in ps.Projects) if (kv.Value.Done) projects++;
            float life = Mathf.Clamp01((clean - 0.5f) / 0.5f);
            float day = 1f - dark;
            float calm = 1f - storm;

            // Vögel / Möwen
            float birdsBase = Mathf.Clamp01(0.35f * life + 0.65f * eco + (projHere ? 0.15f : 0f));
            float birds = 0f, gulls = 0f;
            switch (pl)
            {
                case "terra": birds = birdsBase; break;
                case "pyra": birds = birdsBase * 0.5f; break;
                case "pelagia": gulls = Mathf.Clamp01(0.25f + 0.75f * birdsBase); break;
            }
            birds = CitySmooth("birds", birds * day * calm * shelter, dt);
            gulls = CitySmooth("gulls", gulls * (1f - 0.7f * dark) * calm * shelter, dt);
            AmbLoop("amb_city_birds", "city_birds", birds > 0.01f, birds * 0.75f, 0f);
            AmbLoop("amb_city_gulls", "city_gulls", gulls > 0.01f, gulls * 0.7f, 0f);

            // Blätter im Wind
            float leafMul = pl == "terra" ? 1f : pl == "pelagia" ? 0.8f : 0.35f;
            float leaves = CitySmooth("leaves", eco * leafMul * (0.35f + 0.65f * wind) * (1f - 0.6f * storm) * shelter, dt);
            AmbLoop("amb_city_leaves", "city_leaves", leaves > 0.01f, leaves * 0.6f, 0f);

            // Fernes Stadtleben, sobald die Stadt erwacht
            float wake = projects / 3f;
            float cityT = wake * (1f - 0.6f * dark) * calm * (0.5f + 0.5f * shelter);
            float city = CitySmooth("life", cityT, dt, 4f);
            AmbLoop("amb_city_life", "city_life", city > 0.01f, city * 0.55f, 0f);

            // Brunnen (räumlich)
            if (fountainPlanet != pl)
            {
                fountainPlanet = pl;
                for (int i = 0; i < fountainLoops; i++) LoopInternal("amb_fountain_" + i, null, false, null, 0f, 1f, Cat.Ambient);
                fountainPos.Clear(); fountainLit.Clear();
                foreach (var p in WorldGen.Get(pl).Props)
                    if (p.Kind == "fountain") { fountainPos.Add(new Vector3(p.Pos.x, p.Pos.y + 1.2f, p.Pos.z)); fountainLit.Add(p.LitBy); }
                fountainLoops = Mathf.Max(fountainLoops, fountainPos.Count);
            }
            for (int i = 0; i < fountainPos.Count; i++)
            {
                string lit = fountainLit[i];
                bool on = lit == null || (ps.Projects.TryGetValue(lit, out pst) && pst.Done);
                LoopInternal("amb_fountain_" + i, "city_fountain", on, fountainPos[i], 0.8f, 1f, Cat.Ambient);
            }
        }
    }
}
