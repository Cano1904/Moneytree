using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Stützpunkt: Hauptgebäude mit Lager-Silos, Garage, Stationen als erkennbare Maschinen (Verkaufsterminal,
    /// Werkstatt mit Werkzeugwand, Sortieranlage, Marktstand, Entsorgung mit Gefahrstoffzeichen, Auftragstafel,
    /// Ladeplatz mit Kabeln) – beschildert mit leuchtenden Piktogrammen statt Schrift – und das Transportschiff.
    /// Oberflächen über Materialklassen (Putz, Beton, Lack, Gummi …), Kanten gefast, Nieten, Kabel, Warnstreifen.
    /// Alle Teile unter 2,2 m bleiben innerhalb der Kollisionsbox ihrer Station; Dächer und Schilder ragen darüber hinaus.
    /// </summary>
    public partial class WorldView
    {
        /// <summary>Lampen des Stützpunkts (leuchten nachts immer, unabhängig von den Bereichsprojekten).</summary>
        readonly List<Vector3> baseLamps = new List<Vector3>();
        Material baseLampMat;

        static readonly Dictionary<string, int> StationIcons = new Dictionary<string, int>
        {
            { "sell", SurfaceLook.Icon.Coin }, { "workshop", SurfaceLook.Icon.Gear }, { "sort", SurfaceLook.Icon.Sort },
            { "trader", SurfaceLook.Icon.Bag }, { "disposal", SurfaceLook.Icon.Hazard }, { "contracts", SurfaceLook.Icon.Board },
            { "storage", SurfaceLook.Icon.Crate }, { "charge", SurfaceLook.Icon.Bolt }, { "ship", SurfaceLook.Icon.Ship },
            { "garage", SurfaceLook.Icon.Rover },
        };

        static Color Q(Color c) { return new Color(Mathf.Round(c.r * 20) / 20f, Mathf.Round(c.g * 20) / 20f, Mathf.Round(c.b * 20) / 20f, 1f); }
        /// <summary>Lackiertes Blech (Kratzer, Rost, Kantenabrieb).</summary>
        static Material Paint(Color c, float gloss = 0.45f) { return Mats.Surface(SurfKind.Paint, Q(c), gloss); }
        /// <summary>Blankes/verzinktes Metall.</summary>
        static Material Steel(Color c) { return Mats.Surface(SurfKind.Paint, Q(c), 0.55f, true); }
        static Material Conc(Color c) { return Mats.Surface(SurfKind.Concrete, Q(c)); }
        static Material Rubber(Color c) { return Mats.Surface(SurfKind.Rubber, Q(c), 0.25f); }
        static Material Glow(Color c, float k) { return Mats.Get(Mats.Emissive, c, c * k); }

        // ------------------------------------------------------------------ Schilder
        /// <summary>
        /// Schild mit leuchtendem Piktogramm (Atlas-Zelle <paramref name="icon"/>): farbiger Rahmen, dunkles
        /// Gehäuse mit Schrauben; von vorn (+Z-Seite) und optional von hinten lesbar.
        /// </summary>
        void IconSign(MultiBuilder mb, Vector3 center, float yaw, float size, int icon, Color accent, bool back = true)
        {
            var o = mb.M;
            mb.M = o * Matrix4x4.TRS(center, Quaternion.Euler(0, yaw, 0), Vector3.one);
            float h = size * 0.5f, t = back ? 0.06f : 0.035f;
            mb.For(Paint(accent, 0.5f)).BevelBox(back ? Vector3.zero : new Vector3(0, 0, -0.02f), new Vector3(size + 0.2f, size + 0.2f, back ? 0.08f : 0.05f), 0.035f);
            mb.For(Steel(new Color(0.18f, 0.19f, 0.21f))).BevelBox(Vector3.zero, new Vector3(size + 0.06f, size + 0.06f, t * 2f - 0.01f), 0.015f);
            var ib = mb.For(SurfaceLook.SignMaterial());
            ib.UVRect = SurfaceLook.IconRect(icon);
            ib.Quad(new Vector3(h, -h, t), new Vector3(-h, -h, t), new Vector3(-h, h, t), new Vector3(h, h, t), Vector3.forward);
            if (back)
            {
                ib.UVRect = SurfaceLook.IconRect(icon);
                ib.Quad(new Vector3(-h, -h, -t), new Vector3(h, -h, -t), new Vector3(h, h, -t), new Vector3(-h, h, -t), Vector3.back);
            }
            var bolt = Steel(new Color(0.7f, 0.72f, 0.74f));
            for (int k = 0; k < 4; k++)
            {
                float bx = (k % 2 == 0 ? -1 : 1) * (h + 0.06f), by = (k < 2 ? -1 : 1) * (h + 0.06f);
                mb.For(bolt).Box(new Vector3(bx, by, t + 0.005f), new Vector3(0.04f, 0.04f, 0.02f));
                if (back) mb.For(bolt).Box(new Vector3(bx, by, -t - 0.005f), new Vector3(0.04f, 0.04f, 0.02f));
            }
            mb.M = o;
        }

        /// <summary>Warnstreifen (gelb/schwarz, schräg) als Band der Länge len entlang der lokalen X-Achse.</summary>
        void HazardBand(MultiBuilder mb, Vector3 c, float yaw, float len, float h, float depth)
        {
            var o = mb.M;
            mb.M = o * Matrix4x4.TRS(c, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.For(Paint(new Color(0.95f, 0.75f, 0.12f), 0.4f)).Box(Vector3.zero, new Vector3(len, h, depth));
            int n = Mathf.Max(2, Mathf.RoundToInt(len / (h * 1.4f)));
            float step = len / n;
            for (int k = 0; k < n; k++)
                mb.For(Paint(new Color(0.08f, 0.08f, 0.09f))).BoxRot(new Vector3(-len * 0.5f + (k + 0.5f) * step, 0, depth * 0.5f + 0.004f), new Vector3(step * 0.45f, h * 1.25f, 0.006f), new Vector3(0, 0, 40));
            mb.M = o;
        }

        /// <summary>Durchhängendes Kabel als Rohrkette (Kettenlinie angenähert).</summary>
        static void Cable(MeshBuilder b, Vector3 a, Vector3 c, float sag, float r, int seg = 6)
        {
            var prev = a;
            for (int i = 1; i <= seg; i++)
            {
                float t = i / (float)seg;
                var p = Vector3.Lerp(a, c, t) + Vector3.down * sag * 4f * t * (1f - t);
                b.Tube(prev, p, r, 5);
                prev = p;
            }
        }

        /// <summary>Wandleuchte des Stützpunkts (leuchtet nachts, wirft über den Lichter-Pool echtes Licht).</summary>
        void BaseLamp(MultiBuilder mb, Vector3 p, Vector3 outward)
        {
            if (baseLampMat == null)
            {
                baseLampMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.9f, 0.75f));
                Mats.SetEmission(baseLampMat, new Color(1f, 0.8f, 0.55f) * 0.3f);
            }
            float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            var o = mb.M;
            mb.M = o * Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.For(Steel(new Color(0.2f, 0.21f, 0.23f))).BevelBox(new Vector3(0, 0.02f, 0.14f), new Vector3(0.36f, 0.1f, 0.3f), 0.02f);
            mb.For(Steel(new Color(0.2f, 0.21f, 0.23f))).Box(new Vector3(0, 0.02f, 0.01f), new Vector3(0.12f, 0.18f, 0.04f));
            mb.For(baseLampMat).Box(new Vector3(0, -0.04f, 0.15f), new Vector3(0.28f, 0.03f, 0.22f));
            mb.M = o;
            baseLamps.Add(p + outward * 0.2f + Vector3.down * 0.2f);
        }

        /// <summary>Nachtbeleuchtung des Stützpunkts (von AnimateLights aufgerufen).</summary>
        void AnimateBaseLights(float dark)
        {
            if (baseLampMat != null) Mats.SetEmission(baseLampMat, new Color(1f, 0.8f, 0.55f) * Mathf.Lerp(0.3f, 3.2f, dark));
            AnimateShelters();
        }

        // ================================================================== Stützpunkt
        void BuildBase()
        {
            var b = Layout.Base;
            baseLamps.Clear();
            var mb = new MultiBuilder { UsePalette = true };
            if (trimMat == null) InitBuildingMats();
            float gy = b.Center.y;
            mb.GroundY = gy;
            CoreBuilding(mb, gy);
            HangarInterior(mb, gy);
            GarageBuilding(mb, gy);
            // Ladeplatz
            var ch = b.Stations["charge"];
            ChargeStation(mb, new Vector3(ch.x, gy, ch.z));
            // Landeplatz + Transportschiff
            var pad = b.ShipPad;
            LandingPad(mb, new Vector3(pad.x, gy, pad.z));
            BuildShip(new Vector3(pad.x, gy + 0.2f, pad.z));
            // Stationen
            foreach (var kv in b.Stations)
            {
                uint col;
                if (!StationColors.TryGetValue(kv.Key, out col) || kv.Key == "storage" || kv.Key == "charge") continue;
                var sp = new Vector3(kv.Value.x, gy, kv.Value.z);
                var c = Mats.C(col);
                switch (kv.Key)
                {
                    case "sell": SellTerminal(mb, sp, c); break;
                    case "workshop": Workshop(mb, sp, c); break;
                    case "sort": SortingStation(mb, sp, c); break;
                    case "trader": TraderStall(mb, sp, c); break;
                    case "disposal": DisposalStation(mb, sp, c); break;
                    case "contracts": ContractBoard(mb, sp, c); break;
                    default:
                        mb.For(Paint(new Color(0.2f, 0.21f, 0.23f))).BevelBox(new Vector3(sp.x, gy + 1f, sp.z - 1.2f), new Vector3(1.2f, 2f, 0.8f), 0.05f);
                        break;
                }
                // Piktogramm-Schild über der Station (Unterkante ~2,9 m, frei über dem Roboter) an einem Galgen
                int icon;
                if (!StationIcons.TryGetValue(kv.Key, out icon)) icon = SurfaceLook.Icon.Arrow;
                IconSign(mb, new Vector3(sp.x, gy + 3.5f, sp.z - 1.2f), 0, 1.05f, icon, c);
                var post = Steel(new Color(0.28f, 0.29f, 0.31f));
                mb.For(post).Box(new Vector3(sp.x - 0.62f, gy + 2.55f, sp.z - 1.62f), new Vector3(0.08f, 2.1f, 0.08f));
                mb.For(post).Box(new Vector3(sp.x + 0.62f, gy + 2.55f, sp.z - 1.62f), new Vector3(0.08f, 2.1f, 0.08f));
                mb.For(post).Box(new Vector3(sp.x, gy + 2.9f, sp.z - 1.42f), new Vector3(1.4f, 0.07f, 0.45f));
                mb.For(post).Box(new Vector3(sp.x, gy + 4.12f, sp.z - 1.42f), new Vector3(1.4f, 0.07f, 0.45f));
                // Bodenmarkierung vor der Station: Leuchtstreifen in einer Bodenschiene
                mb.For(Steel(new Color(0.25f, 0.26f, 0.28f))).Box(new Vector3(sp.x, gy + 0.015f, sp.z + 0.1f), new Vector3(1.9f, 0.03f, 0.2f));
                mb.For(Glow(c, 0.7f)).Box(new Vector3(sp.x, gy + 0.03f, sp.z + 0.1f), new Vector3(1.7f, 0.02f, 0.06f));
            }
            // Lager-Annahme: Gitterrost mit Leuchtkante und Warnrand, Förderband ins Hauptgebäude
            var st = b.Stations["storage"];
            var teal = Mats.C(0x2EC4B6);
            mb.For(Steel(new Color(0.22f, 0.23f, 0.25f))).Box(new Vector3(st.x, gy + 0.03f, st.z), new Vector3(5f, 0.06f, 2.5f));
            mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.15f, 0.16f, 0.17f), 0.35f)).Box(new Vector3(st.x, gy + 0.065f, st.z), new Vector3(4.5f, 0.02f, 2.0f));
            foreach (var dz in new[] { -1.15f, 1.15f }) mb.For(Glow(teal, 0.9f)).Box(new Vector3(st.x, gy + 0.07f, st.z + dz), new Vector3(4.6f, 0.02f, 0.05f));
            foreach (var dx in new[] { -2.4f, 2.4f }) mb.For(Glow(teal, 0.9f)).Box(new Vector3(st.x + dx, gy + 0.07f, st.z), new Vector3(0.05f, 0.02f, 2.3f));
            for (int i = 0; i < 9; i++) mb.For(Steel(new Color(0.55f, 0.57f, 0.6f))).CylinderX(new Vector3(st.x - 2.0f + i * 0.5f, gy + 0.11f, st.z - 0.9f), 0.035f, 1.0f, 8);
            mb.For(Rubber(new Color(0.1f, 0.1f, 0.11f))).Box(new Vector3(st.x, gy + 0.15f, st.z - 0.9f), new Vector3(4.2f, 0.02f, 0.9f));
            HazardBand(mb, new Vector3(st.x, gy + 0.075f, st.z + 1.35f), 0, 4.8f, 0.03f, 0.18f);
            mb.Build("Base", Root, true);
            BuildHangarDoor(gy);
        }

        // ------------------------------------------------------------------ Hauptgebäude mit Lager-Silos
        void CoreBuilding(MultiBuilder mb, float gy)
        {
            var white = Mats.Surface(SurfKind.Plaster, new Color(0.9f, 0.88f, 0.82f));
            var tealP = Paint(new Color(0.18f, 0.72f, 0.68f));
            var orange = Paint(new Color(1f, 0.55f, 0.18f));
            var steelD = Steel(new Color(0.24f, 0.25f, 0.27f));
            var conc = Conc(new Color(0.46f, 0.45f, 0.43f));
            const float fz = -141.5f; // Vorderseite (z −149,5 … −141,5)
            // Halle (x −8 … 3.6) als Hangar: Wandscheiben (innen begehbar, Kollision wie WorldGen.BuildHangar), Betonsockel
            // außen, Attika mit Blechabdeckung (ihre Unterseite ist zugleich die Hallendecke)
            mb.For(white).BevelBox(new Vector3(-2.2f, gy + 3.5f, -149.3f), new Vector3(11.6f, 7f, 0.4f), 0.1f);   // Rückwand
            mb.For(white).BevelBox(new Vector3(-7.8f, gy + 3.5f, -145.5f), new Vector3(0.4f, 7f, 8f), 0.1f);      // links
            mb.For(white).BevelBox(new Vector3(3.1f, gy + 3.5f, -145.5f), new Vector3(1.0f, 7f, 8f), 0.1f);       // rechts (zu den Silos)
            mb.For(white).BevelBox(new Vector3(-5.1f, gy + 3.5f, fz - 0.2f), new Vector3(5.0f, 7f, 0.4f), 0.06f); // vorn links
            mb.For(white).Box(new Vector3(0f, gy + 5.65f, fz - 0.2f), new Vector3(5.2f, 2.7f, 0.4f));           // über dem Tor
            mb.For(conc).BevelBox(new Vector3(-2.2f, gy + 0.4f, -149.55f), new Vector3(11.8f, 0.8f, 0.2f), 0.03f);
            mb.For(conc).BevelBox(new Vector3(-8.05f, gy + 0.4f, -145.5f), new Vector3(0.2f, 0.8f, 8.15f), 0.03f);
            mb.For(conc).BevelBox(new Vector3(-5.3f, gy + 0.4f, fz + 0.05f), new Vector3(5.4f, 0.8f, 0.14f), 0.03f);
            mb.For(tealP).BevelBox(new Vector3(-2.2f, gy + 7.2f, -145.5f), new Vector3(12.2f, 0.5f, 8.6f), 0.06f);
            mb.For(steelD).Box(new Vector3(-2.2f, gy + 6.93f, fz - 0.28f), new Vector3(12.2f, 0.06f, 0.1f)); // Tropfkante
            mb.For(orange).Box(new Vector3(-2.2f, gy + 2.6f, fz + 0.03f), new Vector3(11.6f, 0.35f, 0.08f));
            // Lisenen (Wandpfeiler) gliedern die Fassade
            foreach (var x in new[] { -7.9f, -5.2f, 2.9f })
                mb.For(Conc(new Color(0.8f, 0.78f, 0.73f))).BevelBox(new Vector3(x, gy + 3.5f, fz + 0.06f), new Vector3(0.36f, 6.2f, 0.14f), 0.03f);
            // Rolltor-Öffnung (x ±2,6, 4,3 m hoch) hinter der Lager-Annahme: Zarge mit Führungsschienen, Warnkante,
            // Wickelkasten; das Torblatt selbst ist beweglich (HangarDoor) und fährt nachts/bei Sturm hoch
            foreach (var sx in new[] { -2.74f, 2.74f })
            {
                mb.For(steelD).BevelBox(new Vector3(sx, gy + 2.2f, fz + 0.12f), new Vector3(0.26f, 4.4f, 0.2f), 0.03f);
                mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(sx, gy + 0.55f, fz + 0.23f), new Vector3(0.27f, 1.1f, 0.02f));
                mb.For(Glow(new Color(1f, 0.7f, 0.25f), 1.4f)).Box(new Vector3(sx, gy + 3.9f, fz + 0.23f), new Vector3(0.1f, 0.1f, 0.02f)); // Torleuchte
            }
            HazardBand(mb, new Vector3(0, gy + 0.02f, fz - 0.2f), 0, 5.2f, 0.03f, 0.4f); // Schwelle
            mb.For(steelD).BevelBox(new Vector3(0, gy + 4.5f, fz + 0.22f), new Vector3(5.8f, 0.46f, 0.4f), 0.04f); // Wickelkasten
            // Fensterreihe oben mit Faschen, Bänken und Stürzen
            foreach (var x in new[] { -4.0f, 2.4f })
            {
                mb.For(Conc(new Color(0.78f, 0.76f, 0.72f))).Box(new Vector3(x, gy + 5.05f, fz + 0.015f), new Vector3(1.55f, 1.25f, 0.04f));
                mb.For(WindowMat(0)).Box(new Vector3(x, gy + 5.05f, fz + 0.04f), new Vector3(1.3f, 1.0f, 0.06f));
                mb.For(trimMat).Box(new Vector3(x, gy + 4.5f, fz + 0.1f), new Vector3(1.6f, 0.08f, 0.22f));
                mb.For(trimMat).Box(new Vector3(x, gy + 5.65f, fz + 0.06f), new Vector3(1.6f, 0.12f, 0.1f));
            }
            // Tür links mit Zarge, Griff, Vordach und Wandleuchte
            mb.For(Paint(new Color(0.22f, 0.3f, 0.34f))).Box(new Vector3(-4.2f, gy + 1.15f, fz + 0.03f), new Vector3(1.1f, 2.3f, 0.06f));
            mb.For(steelD).Box(new Vector3(-4.2f, gy + 2.35f, fz + 0.06f), new Vector3(1.3f, 0.1f, 0.1f));
            foreach (var sx in new[] { -4.8f, -3.6f }) mb.For(steelD).Box(new Vector3(sx, gy + 1.2f, fz + 0.06f), new Vector3(0.1f, 2.4f, 0.1f));
            mb.For(Steel(new Color(0.75f, 0.76f, 0.78f))).Box(new Vector3(-3.8f, gy + 1.1f, fz + 0.09f), new Vector3(0.18f, 0.04f, 0.05f));
            mb.For(Glow(new Color(0.3f, 1f, 0.6f), 1.2f)).Box(new Vector3(-3.72f, gy + 1.35f, fz + 0.075f), new Vector3(0.06f, 0.1f, 0.02f)); // Kartenleser
            mb.For(tealP).BevelBox(new Vector3(-4.2f, gy + 2.6f, fz + 0.45f), new Vector3(1.8f, 0.1f, 0.9f), 0.03f);
            foreach (var sx in new[] { -4.95f, -3.45f }) mb.For(steelD).Beam(new Vector3(sx, gy + 2.55f, fz + 0.85f), new Vector3(sx, gy + 3.2f, fz + 0.02f), 0.03f);
            BaseLamp(mb, new Vector3(-4.2f, gy + 3.05f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(2.4f, gy + 3.9f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(-2.4f, gy + 4.9f, fz + 0.02f), Vector3.forward);
            // Info-Anzeige (Rahmen, gedämpfter Bildschirm mit Balken) statt grell leuchtender Fläche
            mb.For(steelD).BevelBox(new Vector3(-6.5f, gy + 5.25f, fz + 0.06f), new Vector3(1.9f, 1.2f, 0.12f), 0.04f);
            mb.For(Glow(new Color(0.08f, 0.3f, 0.3f), 0.8f)).Box(new Vector3(-6.5f, gy + 5.25f, fz + 0.125f), new Vector3(1.7f, 1.0f, 0.01f));
            for (int k = 0; k < 4; k++)
                mb.For(Glow(new Color(0.3f, 1f, 0.8f), 1.1f)).Box(new Vector3(-7.05f + k * 0.12f + (k > 1 ? 0.5f : 0f), gy + 5.0f + (k % 2) * 0.12f + (k > 1 ? 0.15f : 0f), fz + 0.133f), new Vector3(k > 1 ? 0.5f : 0.08f, 0.05f, 0.005f));
            mb.For(Glow(new Color(0.95f, 0.8f, 0.3f), 1.2f)).Box(new Vector3(-6.2f, gy + 5.55f, fz + 0.133f), new Vector3(0.9f, 0.08f, 0.005f));
            // Großes Stützpunkt-Emblem (Recycling-Piktogramm) über dem Tor, links und rechts Leuchtbänder
            IconSign(mb, new Vector3(-0.2f, gy + 5.9f, fz + 0.1f), 0, 1.5f, SurfaceLook.Icon.Recycle, new Color(0.18f, 0.72f, 0.68f), false);
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(steelD).Box(new Vector3(-0.2f + sx * 2.3f, gy + 5.9f, fz + 0.05f), new Vector3(2.6f, 0.16f, 0.06f));
                mb.For(Glow(new Color(0.25f, 0.9f, 0.85f), 0.9f)).Box(new Vector3(-0.2f + sx * 2.3f, gy + 5.9f, fz + 0.085f), new Vector3(2.5f, 0.05f, 0.01f));
            }
            // Fallrohre und Kabelkanal
            foreach (var x in new[] { -7.6f, 3.3f })
            {
                mb.For(Steel(new Color(0.55f, 0.57f, 0.58f))).Cylinder(new Vector3(x, gy + 0.1f, fz + 0.14f), 0.07f, 6.9f, 8);
                for (float y = gy + 1.2f; y < gy + 6.8f; y += 1.5f) mb.For(steelD).Box(new Vector3(x, y, fz + 0.08f), new Vector3(0.2f, 0.05f, 0.12f));
            }
            mb.For(steelD).Box(new Vector3(-5.8f, gy + 3.9f, fz + 0.06f), new Vector3(0.14f, 2.2f, 0.1f));
            // Klimageräte an der Seitenwand
            for (int k = 0; k < 2; k++)
            {
                var p = new Vector3(-8.25f, gy + 2.2f + k * 2.2f, -144.5f - k * 2.5f);
                mb.For(Paint(new Color(0.82f, 0.83f, 0.84f))).BevelBox(p, new Vector3(0.45f, 0.7f, 0.95f), 0.04f);
                mb.For(darkMat).CylinderX(p + new Vector3(-0.23f, 0, 0.1f), 0.24f, 0.02f, 12);
                for (int g = 0; g < 4; g++) mb.For(steelD).Box(p + new Vector3(-0.245f, -0.18f + g * 0.12f, 0.1f), new Vector3(0.01f, 0.02f, 0.5f));
                mb.For(Steel(new Color(0.6f, 0.62f, 0.63f))).Tube(p + new Vector3(0, -0.35f, -0.3f), new Vector3(-8.05f, gy + 0.3f, p.z - 0.3f), 0.025f, 5);
            }
            // Dach: Solarmodule (Zellraster), Antennenmast, Klimageräte, Warnleuchte
            var solar = Mats.Surface(SurfKind.Tiles, new Color(0.12f, 0.17f, 0.36f), 0.9f);
            for (int i = 0; i < 3; i++)
            {
                var sp = new Vector3(-6f + i * 2.6f, gy + 7.9f, -146.5f);
                mb.For(solar).BoxRot(sp, new Vector3(2.3f, 0.06f, 3.6f), new Vector3(-22, 0, 0));
                mb.For(Steel(new Color(0.7f, 0.72f, 0.75f))).BoxRot(sp + new Vector3(0, -0.04f, 0), new Vector3(2.38f, 0.05f, 3.68f), new Vector3(-22, 0, 0));
                foreach (var sx in new[] { -1f, 1f }) mb.For(steelD).Beam(sp + new Vector3(sx * 1f, -0.6f, 1.2f), sp + new Vector3(sx * 1f, -0.05f, 1.1f), 0.05f);
            }
            mb.For(railMat).Cylinder(new Vector3(1.8f, gy + 7.4f, -148f), 0.12f, 6f, 6);
            for (int k = 0; k < 3; k++) mb.For(railMat).Beam(new Vector3(1.8f, gy + 7.45f, -148f) + Quaternion.Euler(0, k * 120, 0) * new Vector3(1.2f, 0, 0), new Vector3(1.8f, gy + 10.5f, -148f), 0.03f);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(1.8f, gy + 12.6f, -148f), Quaternion.Euler(-50, 150, 0), Vector3.one);
            mb.For(acMat).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.8f, 0.25f), new Vector2(1.2f, 0.55f), new Vector2(1.25f, 0.6f) }, 16, true);
            mb.For(steelD).Cylinder(Vector3.zero, 0.04f, 1.1f, 5);
            mb.For(steelD).Sphere(new Vector3(0, 1.1f, 0), 0.08f, 6, 4);
            mb.M = o;
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(1.8f, gy + 13.5f, -148f), 0.2f, 6, 4);
            mb.For(Paint(new Color(0.82f, 0.83f, 0.84f))).BevelBox(new Vector3(-1.5f, gy + 7.9f, -143.5f), new Vector3(1.6f, 0.9f, 1.2f), 0.05f);
            mb.For(darkMat).Cylinder(new Vector3(-1.5f, gy + 8.35f, -143.5f), 0.45f, 0.04f, 12);
            for (int g = 0; g < 3; g++) mb.For(steelD).Box(new Vector3(-1.5f, gy + 8.4f, -143.5f), new Vector3(0.9f - g * 0.25f, 0.02f, 0.03f));
            // Lager-Silos (x 4.3 … 7.9) mit Kegeldach, Ringen, Leiter und Rohrbrücke
            var siloM = Mats.Surface(SurfKind.Cladding, new Color(0.88f, 0.88f, 0.85f), 0.4f);
            for (int s = 0; s < 2; s++)
            {
                var p = new Vector3(6.1f, gy, s == 0 ? -143.5f : -147.4f);
                mb.For(conc).Cylinder(p, 1.9f, 0.5f, 20, true);
                mb.For(siloM).Cylinder(p + Vector3.up * 0.5f, 1.75f, 9.5f, 24, false);
                mb.For(tealP).Cylinder(p + Vector3.up * 10f, 1.8f, 1.6f, 24, true, 0.3f);
                mb.For(steelD).Torus(p + Vector3.up * 10f, 1.8f, 0.05f, 24, 4);
                for (float y = 1.5f; y < 10f; y += 2.2f) mb.For(railMat).Torus(p + Vector3.up * y, 1.77f, 0.07f, 24, 4);
                mb.For(orange).Cylinder(p + Vector3.up * 6.5f, 1.78f, 0.6f, 24, false);
                mb.For(railMat).Tube(p + new Vector3(-1.2f, 9.2f, 0), new Vector3(3.2f, gy + 7.6f, p.z), 0.25f, 10);
                mb.For(steelD).Torus(new Vector3(3.9f, gy + 7.95f, p.z), 0.27f, 0.04f, 10, 3);
                // Füllstandsanzeige
                mb.For(steelD).Box(p + new Vector3(0, 3.5f, 1.76f), new Vector3(0.14f, 4f, 0.05f));
                mb.For(Glow(new Color(0.3f, 1f, 0.8f), 0.9f)).Box(p + new Vector3(0, 2.5f, 1.79f), new Vector3(0.06f, 2f, 0.01f));
            }
            mb.For(railMat).Box(new Vector3(7.95f, gy + 5f, -143.5f), new Vector3(0.06f, 10f, 0.06f));
            mb.For(railMat).Box(new Vector3(7.95f, gy + 5f, -142.9f), new Vector3(0.06f, 10f, 0.06f));
            for (float y = 0.4f; y < 10f; y += 0.4f) mb.For(railMat).Box(new Vector3(7.95f, gy + y, -143.2f), new Vector3(0.04f, 0.04f, 0.6f));
            for (float y = 2.4f; y < 10f; y += 1.1f) mb.For(railMat).TorusRot(new Vector3(8.25f, gy + y, -143.2f), Vector3.zero, 0.4f, 0.02f, 10, 3);
            IconSign(mb, new Vector3(6.1f, gy + 3.2f, -141.66f), 0, 0.85f, SurfaceLook.Icon.Crate, new Color(0.18f, 0.77f, 0.71f), false);
        }

        // ------------------------------------------------------------------ Garage
        void GarageBuilding(MultiBuilder mb, float gy)
        {
            var wall = Mats.Surface(SurfKind.Cladding, new Color(0.72f, 0.68f, 0.6f), 0.35f);
            var orange = Paint(new Color(1f, 0.55f, 0.18f));
            var steelD = Steel(new Color(0.24f, 0.25f, 0.27f));
            float fz = -142.5f;
            mb.For(wall).BevelBox(new Vector3(-26, gy + 2.5f, -146), new Vector3(10, 5, 7), 0.08f);
            mb.For(Conc(new Color(0.42f, 0.41f, 0.39f))).BevelBox(new Vector3(-26, gy + 0.35f, -146), new Vector3(10.15f, 0.7f, 7.15f), 0.03f);
            mb.For(orange).BevelBox(new Vector3(-26, gy + 5.2f, -146), new Vector3(10.4f, 0.4f, 7.4f), 0.05f);
            // Sektionaltor mit Paneelen, Fensterband, Zarge und Warnpfosten
            mb.For(darkMat).Box(new Vector3(-26, gy + 2f, fz + 0.02f), new Vector3(7.2f, 4.1f, 0.06f));
            for (int k = 0; k < 5; k++)
            {
                float y = gy + 0.4f + k * 0.8f;
                mb.For(Paint(new Color(0.78f, 0.79f, 0.8f))).BevelBox(new Vector3(-26, y, fz + 0.07f), new Vector3(7f, 0.74f, 0.06f), 0.02f);
                if (k == 3) for (int i = 0; i < 5; i++) mb.For(WindowMat(0)).Box(new Vector3(-28.8f + i * 1.4f, y, fz + 0.11f), new Vector3(0.9f, 0.35f, 0.02f));
                else for (int i = 0; i < 6; i++) mb.For(steelD).Box(new Vector3(-28.9f + i * 1.16f, y, fz + 0.105f), new Vector3(0.02f, 0.6f, 0.01f));
            }
            foreach (var sx in new[] { -29.7f, -22.3f })
            {
                mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).BevelBox(new Vector3(sx, gy + 2f, fz + 0.1f), new Vector3(0.18f, 4f, 0.1f), 0.02f);
                for (int k = 0; k < 6; k++) mb.For(darkMat).BoxRot(new Vector3(sx, gy + 0.3f + k * 0.7f, fz + 0.16f), new Vector3(0.19f, 0.3f, 0.02f), new Vector3(0, 0, 30));
                mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).Cylinder(new Vector3(sx + (sx < -26 ? -0.4f : 0.4f), gy, fz + 0.6f), 0.12f, 0.9f, 10, true, 0.1f); // Anfahrschutz
            }
            // Wandlampen und Piktogramm
            BaseLamp(mb, new Vector3(-30.3f, gy + 4.3f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(-21.7f, gy + 4.3f, fz + 0.02f), Vector3.forward);
            IconSign(mb, new Vector3(-26, gy + 4.55f, fz + 0.1f), 0, 0.72f, SurfaceLook.Icon.Rover, new Color(1f, 0.55f, 0.18f), false);
            // Seitenfenster, Dachlüfter, Fallrohr
            mb.For(Conc(new Color(0.8f, 0.78f, 0.73f))).Box(new Vector3(-31.02f, gy + 2.8f, -146f), new Vector3(0.04f, 1.2f, 2.4f));
            mb.For(WindowMat(0)).Box(new Vector3(-31.05f, gy + 2.8f, -146f), new Vector3(0.06f, 1f, 2.2f));
            mb.For(Steel(new Color(0.7f, 0.72f, 0.74f))).Cylinder(new Vector3(-24f, gy + 5.4f, -147f), 0.4f, 0.8f, 12);
            mb.For(Steel(new Color(0.7f, 0.72f, 0.74f))).Cylinder(new Vector3(-24f, gy + 6.2f, -147f), 0.6f, 0.2f, 12, true, 0.1f);
            mb.For(Steel(new Color(0.55f, 0.57f, 0.58f))).Cylinder(new Vector3(-20.85f, gy, fz - 0.2f), 0.07f, 5.1f, 8);
        }

        // ------------------------------------------------------------------ Stationen
        void SellTerminal(MultiBuilder mb, Vector3 sp, Color c)
        {
            var body = Paint(new Color(0.2f, 0.22f, 0.25f));
            var accent = Paint(c);
            var steelD = Steel(new Color(0.3f, 0.31f, 0.33f));
            var cz = sp.z - 1.2f;
            mb.For(Conc(new Color(0.5f, 0.49f, 0.47f))).BevelBox(new Vector3(sp.x, sp.y + 0.06f, cz), new Vector3(1.38f, 0.12f, 0.98f), 0.03f);
            mb.For(body).BevelBox(new Vector3(sp.x, sp.y + 1.08f, cz), new Vector3(1.3f, 1.95f, 0.9f), 0.07f);
            mb.For(accent).BevelBox(new Vector3(sp.x, sp.y + 2.1f, cz), new Vector3(1.36f, 0.12f, 0.96f), 0.04f);
            // schräger Bildschirm mit Kurs-Balken in einer Blende
            mb.For(steelD).BevelBoxRot(new Vector3(sp.x, sp.y + 1.55f, cz + 0.45f), new Vector3(1.12f, 0.74f, 0.06f), new Vector3(-12, 0, 0), 0.02f);
            mb.For(Glow(c * 0.6f, 1.2f)).BoxRot(new Vector3(sp.x, sp.y + 1.55f, cz + 0.475f), new Vector3(1.0f, 0.62f, 0.02f), new Vector3(-12, 0, 0));
            for (int k = 0; k < 4; k++) mb.For(Glow(new Color(0.95f, 0.85f, 0.4f), 1.4f)).BoxRot(new Vector3(sp.x - 0.3f + k * 0.2f, sp.y + 1.42f + k * 0.04f, cz + 0.49f), new Vector3(0.12f, 0.1f + k * 0.08f, 0.01f), new Vector3(-12, 0, 0));
            // Münzschlitz, Tastenfeld, Ausgabefach, Lüftungsgitter, Leuchtstreifen
            mb.For(Steel(new Color(0.72f, 0.74f, 0.76f))).BevelBox(new Vector3(sp.x + 0.35f, sp.y + 1.05f, cz + 0.45f), new Vector3(0.3f, 0.4f, 0.04f), 0.01f);
            for (int k = 0; k < 9; k++) mb.For(signLight).BevelBox(new Vector3(sp.x + 0.27f + (k % 3) * 0.08f, sp.y + 0.95f + (k / 3) * 0.08f, cz + 0.48f), new Vector3(0.055f, 0.055f, 0.02f), 0.008f);
            mb.For(darkMat).Box(new Vector3(sp.x - 0.25f, sp.y + 0.6f, cz + 0.46f), new Vector3(0.6f, 0.25f, 0.04f));
            mb.For(Steel(new Color(0.72f, 0.74f, 0.76f))).Box(new Vector3(sp.x - 0.25f, sp.y + 0.49f, cz + 0.47f), new Vector3(0.62f, 0.03f, 0.05f));
            for (int k = 0; k < 6; k++) mb.For(darkMat).Box(new Vector3(sp.x - 0.25f, sp.y + 1.08f + k * 0.045f, cz + 0.455f), new Vector3(0.5f, 0.018f, 0.01f));
            mb.For(Glow(c, 1.1f)).Box(new Vector3(sp.x - 0.66f, sp.y + 1.05f, cz + 0.2f), new Vector3(0.02f, 1.6f, 0.06f));
            mb.For(Glow(c, 1.1f)).Box(new Vector3(sp.x + 0.66f, sp.y + 1.05f, cz + 0.2f), new Vector3(0.02f, 1.6f, 0.06f));
            // Schrauben an den Seitenblechen
            for (int k = 0; k < 4; k++) foreach (var sx in new[] { -0.655f, 0.655f }) mb.For(Steel(new Color(0.6f, 0.62f, 0.64f))).Box(new Vector3(sp.x + sx, sp.y + 0.3f + k * 0.5f, cz - 0.3f), new Vector3(0.012f, 0.035f, 0.035f));
            // Credit-Symbol oben
            mb.For(Glow(new Color(1f, 0.8f, 0.3f), 1.5f)).TorusRot(new Vector3(sp.x, sp.y + 2.5f, cz), new Vector3(90, 0, 0), 0.2f, 0.045f, 16, 5);
            mb.For(steelD).Cylinder(new Vector3(sp.x, sp.y + 2.16f, cz), 0.03f, 0.14f, 5);
        }

        void Workshop(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Paint(c);
            var wood = woodMat;
            var steelD = Steel(new Color(0.3f, 0.32f, 0.35f));
            // Rückwand mit Lochblech und Werkzeugen
            mb.For(Paint(new Color(0.3f, 0.32f, 0.35f))).BevelBox(new Vector3(sp.x, sp.y + 1.25f, cz - 0.42f), new Vector3(1.4f, 2.5f, 0.16f), 0.03f);
            mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.75f, 0.72f, 0.62f), 0.3f)).Box(new Vector3(sp.x, sp.y + 1.6f, cz - 0.33f), new Vector3(1.3f, 1.0f, 0.02f));
            var tool = MetalMat(new Color(0.75f, 0.77f, 0.8f));
            mb.For(tool).BoxRot(new Vector3(sp.x - 0.45f, sp.y + 1.65f, cz - 0.31f), new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0, 0, 8));
            mb.For(tool).TorusRot(new Vector3(sp.x - 0.43f, sp.y + 1.95f, cz - 0.31f), new Vector3(90, 0, 0), 0.07f, 0.02f, 10, 4);
            mb.For(accent).Box(new Vector3(sp.x - 0.18f, sp.y + 1.45f, cz - 0.31f), new Vector3(0.06f, 0.3f, 0.03f));
            mb.For(tool).Box(new Vector3(sp.x - 0.18f, sp.y + 1.72f, cz - 0.31f), new Vector3(0.22f, 0.1f, 0.04f));
            mb.For(Paint(new Color(0.85f, 0.2f, 0.18f))).Box(new Vector3(sp.x + 0.12f, sp.y + 1.5f, cz - 0.31f), new Vector3(0.05f, 0.35f, 0.03f));
            mb.For(tool).BoxRot(new Vector3(sp.x + 0.12f, sp.y + 1.75f, cz - 0.31f), new Vector3(0.06f, 0.2f, 0.03f), new Vector3(0, 0, 90));
            for (int k = 0; k < 4; k++) mb.For(tool).Box(new Vector3(sp.x + 0.36f + k * 0.07f, sp.y + 1.55f - k * 0.03f, cz - 0.31f), new Vector3(0.03f, 0.3f + k * 0.05f, 0.02f));
            // Werkbank mit Schraubstock und roter Werkzeugkiste (Schubladen mit Griffen)
            mb.For(wood).BevelBox(new Vector3(sp.x, sp.y + 0.95f, cz + 0.05f), new Vector3(1.4f, 0.08f, 0.8f), 0.015f);
            mb.For(railMat).Box(new Vector3(sp.x - 0.62f, sp.y + 0.46f, cz + 0.05f), new Vector3(0.08f, 0.92f, 0.7f));
            mb.For(railMat).Box(new Vector3(sp.x + 0.62f, sp.y + 0.46f, cz + 0.05f), new Vector3(0.08f, 0.92f, 0.7f));
            mb.For(Paint(new Color(0.8f, 0.18f, 0.15f), 0.55f)).BevelBox(new Vector3(sp.x + 0.2f, sp.y + 0.45f, cz - 0.05f), new Vector3(0.7f, 0.8f, 0.5f), 0.03f);
            for (int k = 0; k < 4; k++)
            {
                mb.For(steelD).Box(new Vector3(sp.x + 0.2f, sp.y + 0.2f + k * 0.18f, cz + 0.205f), new Vector3(0.64f, 0.012f, 0.01f));
                mb.For(Steel(new Color(0.75f, 0.76f, 0.78f))).Box(new Vector3(sp.x + 0.2f, sp.y + 0.28f + k * 0.18f, cz + 0.215f), new Vector3(0.2f, 0.025f, 0.02f));
            }
            mb.For(tool).BevelBox(new Vector3(sp.x - 0.4f, sp.y + 1.08f, cz + 0.3f), new Vector3(0.22f, 0.18f, 0.2f), 0.02f);
            mb.For(tool).Box(new Vector3(sp.x - 0.4f, sp.y + 1.12f, cz + 0.44f), new Vector3(0.05f, 0.05f, 0.25f));
            mb.For(accent).Crumple(new Vector3(sp.x + 0.35f, sp.y + 1.05f, cz + 0.25f), 0.1f, 0.6f, 3, 0.2f, 6, 3);
            // Garagentor-Front als Dach (über 2,4 m): Rolltor-Kasten mit Lamellen und Warnstreifen, Arbeitsleuchte
            mb.For(Paint(new Color(0.74f, 0.76f, 0.78f))).BevelBox(new Vector3(sp.x, sp.y + 2.55f, cz - 0.1f), new Vector3(2.6f, 0.4f, 0.9f), 0.04f);
            for (int k = 0; k < 3; k++) mb.For(railMat).Box(new Vector3(sp.x, sp.y + 2.42f + k * 0.12f, cz + 0.36f), new Vector3(2.6f, 0.03f, 0.02f));
            HazardBand(mb, new Vector3(sp.x, sp.y + 2.3f, cz + 0.36f), 0, 2.6f, 0.12f, 0.02f);
            mb.For(Glow(new Color(1f, 0.92f, 0.8f), 1.2f)).Box(new Vector3(sp.x, sp.y + 2.33f, cz + 0.1f), new Vector3(1.0f, 0.03f, 0.18f));
        }

        void SortingStation(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Paint(c);
            var frame = Steel(new Color(0.34f, 0.35f, 0.37f));
            // Rahmen, schräges Sortierband mit Rollen, drei farbige Behälter
            foreach (var sx in new[] { -0.64f, 0.64f })
            {
                mb.For(frame).BevelBox(new Vector3(sp.x + sx, sp.y + 0.75f, cz), new Vector3(0.08f, 1.5f, 0.9f), 0.015f);
                mb.For(frame).Box(new Vector3(sp.x + sx, sp.y + 0.03f, cz), new Vector3(0.14f, 0.06f, 0.96f));
            }
            mb.For(Rubber(new Color(0.12f, 0.12f, 0.13f))).BoxRot(new Vector3(sp.x, sp.y + 1.25f, cz - 0.1f), new Vector3(1.2f, 0.06f, 0.8f), new Vector3(-18, 0, 0));
            for (int k = 0; k < 5; k++) mb.For(acMat).CylinderX(new Vector3(sp.x, sp.y + 1.22f + (k - 2) * 0.05f, cz - 0.1f - (k - 2) * 0.15f), 0.04f, 1.2f, 8);
            var bins = new[] { new Color(0.25f, 0.55f, 0.85f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.7f, 0.35f) };
            for (int k = 0; k < 3; k++)
            {
                var p = new Vector3(sp.x - 0.42f + k * 0.42f, sp.y, cz + 0.25f);
                mb.For(Mats.Surface(SurfKind.Rubber, bins[k], 0.45f)).BevelBox(p + Vector3.up * 0.3f, new Vector3(0.38f, 0.6f, 0.38f), 0.03f);
                mb.For(darkMat).Box(p + Vector3.up * 0.595f, new Vector3(0.32f, 0.02f, 0.32f));
                mb.For(Mats.Surface(SurfKind.Rubber, bins[k] * 0.8f, 0.45f)).Box(p + new Vector3(0, 0.6f, -0.02f), new Vector3(0.4f, 0.03f, 0.4f));
                mb.For(signLight).Box(p + new Vector3(0, 0.35f, 0.195f), new Vector3(0.16f, 0.16f, 0.01f));
            }
            // Trichter oben mit dezentem Leuchtring
            mb.For(accent).Cylinder(new Vector3(sp.x, sp.y + 1.55f, cz - 0.15f), 0.18f, 0.7f, 16, false, 0.55f);
            mb.For(frame).Torus(new Vector3(sp.x, sp.y + 2.25f, cz - 0.15f), 0.56f, 0.035f, 20, 4);
            mb.For(Glow(c, 1.0f)).Torus(new Vector3(sp.x, sp.y + 2.21f, cz - 0.15f), 0.53f, 0.02f, 20, 4);
            mb.For(Glow(c, 0.9f)).Box(new Vector3(sp.x, sp.y + 1.6f, cz + 0.46f), new Vector3(0.5f, 0.12f, 0.02f));
        }

        void TraderStall(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Paint(c);
            // Theke mit Waren
            mb.For(woodMat).BevelBox(new Vector3(sp.x, sp.y + 0.5f, cz + 0.1f), new Vector3(1.4f, 1.0f, 0.7f), 0.03f);
            for (int k = 0; k < 5; k++) mb.For(Mats.Surface(SurfKind.Wood, new Color(0.38f, 0.27f, 0.18f))).Box(new Vector3(sp.x - 0.56f + k * 0.28f, sp.y + 0.5f, cz + 0.455f), new Vector3(0.03f, 0.95f, 0.02f));
            mb.For(accent).BevelBox(new Vector3(sp.x, sp.y + 1.02f, cz + 0.1f), new Vector3(1.45f, 0.06f, 0.76f), 0.02f);
            var goods = new[] { new Color(0.72f, 0.45f, 0.25f), new Color(0.6f, 0.75f, 0.85f), new Color(0.35f, 0.65f, 0.4f), new Color(0.8f, 0.8f, 0.82f) };
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector3(sp.x - 0.5f + k * 0.33f, sp.y + 1.15f, cz + 0.05f);
                if (k % 2 == 0) mb.For(Mat(goods[k])).BoxJ(p, new Vector3(0.24f, 0.2f, 0.24f), new Vector3(0, k * 20, 0), 0.02f, k);
                else mb.For(MetalMat(goods[k])).CylinderX(p, 0.08f, 0.26f, 10);
            }
            // Kisten hinter der Theke, Pfosten und gestreifte Markise mit Volant
            mb.For(woodMat).BevelBox(new Vector3(sp.x - 0.35f, sp.y + 0.3f, cz - 0.38f), new Vector3(0.6f, 0.6f, 0.2f), 0.02f);
            mb.For(railMat).Box(new Vector3(sp.x - 0.66f, sp.y + 1.3f, cz - 0.45f), new Vector3(0.07f, 2.6f, 0.07f));
            mb.For(railMat).Box(new Vector3(sp.x + 0.66f, sp.y + 1.3f, cz - 0.45f), new Vector3(0.07f, 2.6f, 0.07f));
            for (int k = 0; k < 6; k++)
            {
                var m = k % 2 == 0 ? accent : signLight;
                mb.For(m).BoxRot(new Vector3(sp.x - 1.25f + k * 0.5f, sp.y + 2.55f, cz + 0.25f), new Vector3(0.5f, 0.04f, 1.6f), new Vector3(-14, 0, 0));
                mb.For(m).Box(new Vector3(sp.x - 1.25f + k * 0.5f, sp.y + 2.28f, cz + 1.03f), new Vector3(0.5f, 0.16f, 0.02f));
            }
            mb.For(Glow(new Color(1f, 0.8f, 0.5f), 1.4f)).Sphere(new Vector3(sp.x + 0.5f, sp.y + 2.25f, cz + 0.4f), 0.1f, 8, 5);
            mb.For(railMat).Cylinder(new Vector3(sp.x + 0.5f, sp.y + 2.33f, cz + 0.4f), 0.01f, 0.2f, 4);
        }

        void DisposalStation(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var yellow = Paint(new Color(0.95f, 0.75f, 0.15f));
            // Gefahrstoff-Container mit Sicken, Warnstreifen und Klappdeckel mit Scharnieren
            mb.For(yellow).BevelBox(new Vector3(sp.x, sp.y + 0.65f, cz), new Vector3(1.35f, 1.3f, 0.95f), 0.05f);
            for (int k = 0; k < 3; k++) mb.For(yellow).BevelBox(new Vector3(sp.x - 0.45f + k * 0.45f, sp.y + 0.7f, cz + 0.48f), new Vector3(0.28f, 1.0f, 0.03f), 0.01f);
            HazardBand(mb, new Vector3(sp.x, sp.y + 0.15f, cz + 0.48f), 0, 1.35f, 0.2f, 0.02f);
            mb.For(Paint(new Color(0.85f, 0.25f, 0.18f))).BevelBoxRot(new Vector3(sp.x, sp.y + 1.42f, cz - 0.1f), new Vector3(1.38f, 0.08f, 0.98f), new Vector3(-12, 0, 0), 0.02f);
            for (int k = 0; k < 2; k++) mb.For(railMat).CylinderX(new Vector3(sp.x - 0.4f + k * 0.8f, sp.y + 1.33f, cz - 0.52f), 0.035f, 0.25f, 8);
            mb.For(acMat).Box(new Vector3(sp.x, sp.y + 1.1f, cz + 0.49f), new Vector3(0.6f, 0.08f, 0.04f));
            // Gefahrstoffzeichen auf der Front, Piktogramm-Schild ist oben
            HazardDiamond(mb, new Vector3(sp.x + 0.35f, sp.y + 0.8f, cz + 0.5f), 0.32f);
            mb.For(railMat).Box(new Vector3(sp.x - 0.55f, sp.y + 1.9f, cz - 0.3f), new Vector3(0.07f, 1.2f, 0.07f));
            HazardDiamond(mb, new Vector3(sp.x - 0.55f, sp.y + 2.35f, cz - 0.25f), 0.5f);
            mb.For(Glow(c, 1.1f)).Box(new Vector3(sp.x, sp.y + 1.2f, cz + 0.5f), new Vector3(1.2f, 0.04f, 0.02f));
            // Rollen
            for (int k = 0; k < 4; k++) mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).CylinderX(new Vector3(sp.x + (k % 2 == 0 ? -0.5f : 0.5f), sp.y + 0.06f, cz + (k < 2 ? -0.35f : 0.35f)), 0.06f, 0.06f, 8);
        }

        void HazardDiamond(MultiBuilder mb, Vector3 p, float s)
        {
            mb.For(Paint(new Color(0.85f, 0.15f, 0.12f))).BoxRot(p, new Vector3(s, s, 0.03f), new Vector3(0, 0, 45));
            mb.For(signLight).BoxRot(p + new Vector3(0, 0, 0.02f), new Vector3(s * 0.78f, s * 0.78f, 0.02f), new Vector3(0, 0, 45));
            mb.For(darkMat).Box(p + new Vector3(0, s * 0.12f, 0.035f), new Vector3(s * 0.12f, s * 0.42f, 0.01f));
            mb.For(darkMat).Box(p + new Vector3(0, -s * 0.28f, 0.035f), new Vector3(s * 0.12f, s * 0.12f, 0.01f));
        }

        void ContractBoard(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            mb.For(woodMat).BevelBox(new Vector3(sp.x - 0.62f, sp.y + 1.1f, cz), new Vector3(0.1f, 2.2f, 0.1f), 0.015f);
            mb.For(woodMat).BevelBox(new Vector3(sp.x + 0.62f, sp.y + 1.1f, cz), new Vector3(0.1f, 2.2f, 0.1f), 0.015f);
            mb.For(Mats.Surface(SurfKind.Wood, new Color(0.55f, 0.4f, 0.28f))).BevelBox(new Vector3(sp.x, sp.y + 1.55f, cz), new Vector3(1.3f, 1.05f, 0.08f), 0.02f);
            mb.For(Mats.Surface(SurfKind.Rubber, new Color(0.62f, 0.48f, 0.33f))).Box(new Vector3(sp.x, sp.y + 1.55f, cz + 0.045f), new Vector3(1.18f, 0.93f, 0.01f)); // Kork
            mb.For(Paint(c)).BevelBox(new Vector3(sp.x, sp.y + 2.12f, cz + 0.05f), new Vector3(1.45f, 0.12f, 0.3f), 0.03f);
            var papers = new[] { signLight, Mat(new Color(0.98f, 0.9f, 0.55f)), Mat(new Color(0.7f, 0.85f, 0.95f)) };
            for (int k = 0; k < 7; k++)
            {
                var p = new Vector3(sp.x - 0.45f + (k % 4) * 0.3f + (k / 4) * 0.12f, sp.y + 1.78f - (k / 4) * 0.45f, cz + 0.055f);
                mb.For(papers[k % 3]).BoxRot(p, new Vector3(0.22f, 0.3f, 0.01f), new Vector3(0, 0, (k * 7 % 11) - 5));
                mb.For(Paint(new Color(0.85f, 0.2f, 0.18f))).Box(p + new Vector3(0, 0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.02f));
                for (int l = 0; l < 3; l++) mb.For(darkMat).BoxRot(p + new Vector3(0, 0.05f - l * 0.06f, 0.008f), new Vector3(0.15f, 0.012f, 0.005f), new Vector3(0, 0, (k * 7 % 11) - 5));
            }
            mb.For(Glow(c, 1.0f)).Box(new Vector3(sp.x, sp.y + 1.0f, cz + 0.05f), new Vector3(1.2f, 0.04f, 0.02f));
            mb.For(Glow(new Color(1f, 0.9f, 0.7f), 1.2f)).Box(new Vector3(sp.x, sp.y + 2.05f, cz + 0.18f), new Vector3(1.0f, 0.02f, 0.04f)); // Leiste beleuchtet die Zettel
        }

        void ChargeStation(MultiBuilder mb, Vector3 p)
        {
            var mint = new Color(0.35f, 1f, 0.75f);
            var steelD = Steel(new Color(0.22f, 0.23f, 0.25f));
            // Ladefläche: Betonplatte mit Gitterfeld, dezenter Leuchtring aus Segmenten, Richtungspfeile
            mb.For(Conc(new Color(0.34f, 0.34f, 0.35f))).Cylinder(p + Vector3.up * 0.005f, 2.6f, 0.04f, 32);
            mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.18f, 0.19f, 0.2f), 0.4f)).Cylinder(p + Vector3.up * 0.01f, 2.3f, 0.04f, 32);
            mb.For(steelD).Torus(p + Vector3.up * 0.05f, 2.45f, 0.05f, 40, 4);
            var ring = Glow(mint, 0.55f);
            for (int k = 0; k < 24; k++)
            {
                float ang = (k + 0.5f) / 24f * 360f;
                mb.For(ring).BoxRot(p + Quaternion.Euler(0, ang, 0) * new Vector3(0, 0.06f, 2.45f), new Vector3(0.4f, 0.02f, 0.035f), new Vector3(0, ang, 0));
            }
            for (int k = 0; k < 3; k++)
            {
                var ap = p + new Vector3(0, 0.055f, -0.9f + k * 0.6f);
                mb.For(Glow(mint, 0.45f)).BoxRot(ap + new Vector3(-0.2f, 0, 0), new Vector3(0.5f, 0.01f, 0.1f), new Vector3(0, 35, 0));
                mb.For(Glow(mint, 0.45f)).BoxRot(ap + new Vector3(0.2f, 0, 0), new Vector3(0.5f, 0.01f, 0.1f), new Vector3(0, -35, 0));
            }
            // Ladesäule an der Wand des Hauptgebäudes: gefastes Gehäuse, Anzeige, Kabeltrommel, Stecker im Halter
            var post = new Vector3(p.x, p.y, -141.3f);
            mb.For(Paint(new Color(0.9f, 0.88f, 0.82f), 0.5f)).BevelBox(post + Vector3.up * 1.1f, new Vector3(0.7f, 2.2f, 0.45f), 0.06f);
            mb.For(steelD).BevelBox(post + new Vector3(0, 1.55f, 0.2f), new Vector3(0.52f, 0.42f, 0.06f), 0.02f);
            mb.For(Glow(new Color(0.12f, 0.4f, 0.3f), 1f)).Box(post + new Vector3(0, 1.55f, 0.235f), new Vector3(0.44f, 0.34f, 0.01f));
            mb.For(Glow(mint, 1.3f)).BoxRot(post + new Vector3(0, 1.57f, 0.242f), new Vector3(0.06f, 0.2f, 0.005f), new Vector3(0, 0, 25));
            mb.For(Glow(mint, 1.3f)).BoxRot(post + new Vector3(0.02f, 1.5f, 0.242f), new Vector3(0.06f, 0.14f, 0.005f), new Vector3(0, 0, -30));
            mb.For(Paint(new Color(0.18f, 0.72f, 0.68f))).BevelBox(post + new Vector3(0, 2.25f, 0), new Vector3(0.75f, 0.1f, 0.5f), 0.02f);
            mb.For(steelD).CylinderZ(post + new Vector3(0, 0.8f, 0.3f), 0.25f, 0.18f, 16);
            mb.For(Steel(new Color(0.65f, 0.66f, 0.68f))).CylinderZ(post + new Vector3(0, 0.8f, 0.3f), 0.08f, 0.22f, 10);
            mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Torus(post + new Vector3(0, 0.8f, 0.3f), 0.2f, 0.035f, 16, 4);
            mb.For(steelD).Box(post + new Vector3(0.28f, 1.15f, 0.25f), new Vector3(0.08f, 0.16f, 0.1f));
            BaseLamp(mb, post + new Vector3(0, 2.45f, 0.1f), Vector3.forward);
            var cable = Rubber(new Color(0.1f, 0.1f, 0.1f));
            var a = post + new Vector3(0.1f, 0.8f, 0.4f);
            var pts = new[] { a, post + new Vector3(0.2f, 0.05f, 0.7f), p + new Vector3(0.9f, 0.05f, -1.2f), p + new Vector3(0.3f, 0.05f, -0.5f), p + new Vector3(0.2f, 0.06f, 0.2f) };
            for (int k = 0; k < pts.Length - 1; k++) mb.For(cable).Tube(pts[k], pts[k + 1], 0.045f, 8);
            var pts2 = new[] { post + new Vector3(-0.2f, 0.8f, 0.3f), post + new Vector3(-0.35f, 0.05f, 0.6f), p + new Vector3(-1.1f, 0.05f, -1.0f), p + new Vector3(-0.6f, 0.05f, 0.4f) };
            for (int k = 0; k < pts2.Length - 1; k++) mb.For(Rubber(new Color(0.95f, 0.5f, 0.15f))).Tube(pts2[k], pts2[k + 1], 0.035f, 8);
            mb.For(Paint(new Color(0.3f, 0.32f, 0.34f))).BevelBox(p + new Vector3(0.2f, 0.1f, 0.25f), new Vector3(0.18f, 0.12f, 0.25f), 0.02f);
            // Rammschutz-Poller mit Warnringen
            foreach (var dx in new[] { -0.75f, 0.75f })
            {
                var bp = new Vector3(p.x + dx, p.y, -140.55f);
                mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).RoundCylinder(bp, 0.1f, 1.0f, 0.05f, 12);
                for (int k = 0; k < 2; k++) mb.For(Paint(new Color(0.08f, 0.08f, 0.09f))).Cylinder(bp + Vector3.up * (0.45f + k * 0.25f), 0.103f, 0.1f, 12, false);
            }
        }

        void LandingPad(MultiBuilder mb, Vector3 p)
        {
            var yel = Paint(new Color(0.95f, 0.8f, 0.2f), 0.3f);
            mb.For(Conc(new Color(0.3f, 0.3f, 0.31f))).Cylinder(p, 6.5f, 0.15f, 40);
            mb.For(Steel(new Color(0.25f, 0.26f, 0.27f))).Torus(p + Vector3.up * 0.1f, 6.52f, 0.08f, 40, 4);
            mb.For(yel).Torus(p + Vector3.up * 0.16f, 5.8f, 0.12f, 40, 4);
            // gestrichelter Innenkreis und Mittelkreuz
            for (int k = 0; k < 20; k++)
            {
                float a = k / 20f * 360f;
                mb.For(Paint(new Color(0.85f, 0.87f, 0.88f), 0.3f)).BoxRot(p + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.155f, 3.2f), new Vector3(0.55f, 0.01f, 0.12f), new Vector3(0, a, 0));
            }
            mb.For(Paint(new Color(0.85f, 0.87f, 0.88f), 0.3f)).Box(p + new Vector3(0, 0.155f, 0), new Vector3(2.2f, 0.01f, 0.25f));
            mb.For(Paint(new Color(0.85f, 0.87f, 0.88f), 0.3f)).Box(p + new Vector3(0, 0.155f, 0), new Vector3(0.25f, 0.01f, 2.2f));
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                var lp = p + new Vector3(Mathf.Cos(a) * 6.2f, 0.15f, Mathf.Sin(a) * 6.2f);
                mb.For(Steel(new Color(0.3f, 0.31f, 0.33f))).Cylinder(lp, 0.16f, 0.06f, 10);
                mb.For(Glow(k % 2 == 0 ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.7f, 0.3f), 1.6f)).Cylinder(lp + Vector3.up * 0.06f, 0.1f, 0.05f, 10, true, 0.07f);
            }
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0, k * 90 + 15, 0);
                // Pfeilwinkel (Chevrons) zeigen zur Mitte
                mb.For(yel).BoxRot(p + d * new Vector3(-0.35f, 0.16f, 4.4f), new Vector3(0.9f, 0.02f, 0.22f), new Vector3(0, k * 90 + 15 + 35, 0));
                mb.For(yel).BoxRot(p + d * new Vector3(0.35f, 0.16f, 4.4f), new Vector3(0.9f, 0.02f, 0.22f), new Vector3(0, k * 90 + 15 - 35, 0));
            }
            // Piktogramm-Schild am Rand (zeigt zur Basis; neben der Rampe, nicht im Weg)
            var sp = p + new Vector3(-7.5f, 0, 5.5f);
            mb.For(Steel(new Color(0.3f, 0.31f, 0.33f))).Cylinder(sp, 0.06f, 2.6f, 8);
            IconSign(mb, sp + new Vector3(0, 2.9f, 0), -60f, 0.8f, SurfaceLook.Icon.Ship, new Color(0.3f, 0.75f, 1f));
        }

        // ------------------------------------------------------------------ Hangar (Innenraum des Hauptgebäudes)
        Transform hangarDoor, shipRamp;
        Light hangarLight, shipLight;
        float hangarOpen, rampOpen;
        bool hangarMoving, rampMoving;
        Quaternion shipYawQ = Quaternion.identity;

        /// <summary>
        /// Einrichtung des Hangars (x −7,6…2,6 / z −149,1…−141,9, lichte Höhe bis zur Attika): Betonboden mit Stellplatz-
        /// Markierung, Werkbank mit Lochwand (Rückwand), Regalwand mit Kisten und Fässern (links), Ladesäule mit Ladering
        /// (rechts hinten), Fachwerkbinder mit warmen Hängeleuchten, Sockelband. Werkbank, Regal und Ladesäule stehen genau in
        /// ihren Kollisionsboxen (WorldGen.BuildHangar); die Mitte bleibt für MIKO und den Rover frei.
        /// </summary>
        void HangarInterior(MultiBuilder mb, float gy)
        {
            var steelD = Steel(new Color(0.24f, 0.25f, 0.27f));
            var steelL = Steel(new Color(0.62f, 0.64f, 0.66f));
            var yellow = Paint(new Color(0.95f, 0.78f, 0.18f), 0.35f);
            var teal = Paint(new Color(0.18f, 0.72f, 0.68f));
            var wood = Mats.Surface(SurfKind.Wood, new Color(0.55f, 0.4f, 0.26f), 0.3f);
            var warm = Glow(new Color(1f, 0.86f, 0.62f), 2.2f);
            const float x0 = -7.6f, x1 = 2.6f, z0 = -149.1f, z1 = -141.9f, cx = -2.5f, cz = -145.5f;
            // Boden: glatter Beton, Stellplatz (gelbe Linien) und Fahrgasse vom Tor
            mb.For(Conc(new Color(0.52f, 0.52f, 0.5f))).Box(new Vector3(cx, gy + 0.015f, cz + 0.2f), new Vector3(x1 - x0, 0.03f, z1 - z0 + 0.4f));
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(yellow).Box(new Vector3(-3.2f + sx * 2.3f, gy + 0.035f, -145.9f), new Vector3(0.1f, 0.01f, 4.6f));
                mb.For(yellow).Box(new Vector3(-3.2f, gy + 0.035f, -145.9f + sx * 2.3f), new Vector3(4.7f, 0.01f, 0.1f));
                mb.For(Paint(new Color(0.85f, 0.87f, 0.88f), 0.3f)).Box(new Vector3(sx * 1.6f, gy + 0.035f, -142.9f), new Vector3(0.12f, 0.01f, 1.8f));
            }
            // Sockelband und Leuchtleiste an den Innenwänden
            mb.For(teal).Box(new Vector3(cx, gy + 1.0f, z0 + 0.02f), new Vector3(x1 - x0, 0.22f, 0.04f));
            mb.For(teal).Box(new Vector3(x0 + 0.02f, gy + 1.0f, cz), new Vector3(0.04f, 0.22f, z1 - z0));
            mb.For(teal).Box(new Vector3(x1 - 0.02f, gy + 1.0f, cz), new Vector3(0.04f, 0.22f, z1 - z0));
            mb.For(Conc(new Color(0.38f, 0.38f, 0.37f))).Box(new Vector3(cx, gy + 0.15f, z0 + 0.03f), new Vector3(x1 - x0, 0.3f, 0.06f));
            // Innenseite der Torwand: Zarge und Torantrieb
            foreach (var sx in new[] { -2.7f, 2.7f }) mb.For(steelD).Box(new Vector3(sx, gy + 2.15f, z1 - 0.06f), new Vector3(0.2f, 4.3f, 0.12f));
            mb.For(steelD).BevelBox(new Vector3(0, gy + 4.45f, z1 - 0.12f), new Vector3(5.6f, 0.3f, 0.2f), 0.03f);
            mb.For(Paint(new Color(0.3f, 0.32f, 0.35f))).BevelBox(new Vector3(2.35f, gy + 4.45f, z1 - 0.25f), new Vector3(0.4f, 0.4f, 0.3f), 0.03f); // Motor
            mb.For(Glow(new Color(0.3f, 1f, 0.5f), 1.5f)).Box(new Vector3(2.35f, gy + 4.45f, z1 - 0.41f), new Vector3(0.06f, 0.06f, 0.01f));
            // Fachwerkbinder unter der Decke mit Hängeleuchten
            for (int k = 0; k < 4; k++)
            {
                float z = -148.2f + k * 1.75f;
                mb.For(steelD).Box(new Vector3(cx, gy + 6.75f, z), new Vector3(x1 - x0, 0.3f, 0.16f));
                mb.For(steelD).Box(new Vector3(cx, gy + 6.2f, z), new Vector3(x1 - x0, 0.1f, 0.12f));
                for (float x = x0 + 0.8f; x < x1 - 0.5f; x += 1.3f)
                    mb.For(steelD).BoxRot(new Vector3(x, gy + 6.47f, z), new Vector3(0.06f, 0.62f, 0.06f), new Vector3(0, 0, (int)((x - x0) / 1.3f) % 2 == 0 ? 35 : -35));
                if (k % 2 == 1) continue;
                foreach (float lx in new[] { -5.3f, -0.4f })
                {
                    mb.For(steelD).Cylinder(new Vector3(lx, gy + 5.55f, z), 0.015f, 0.65f, 4);
                    mb.For(Paint(new Color(0.2f, 0.36f, 0.34f))).Cylinder(new Vector3(lx, gy + 5.3f, z), 0.45f, 0.25f, 14, true, 0.12f);
                    mb.For(warm).Disc(new Vector3(lx, gy + 5.29f, z), 0.4f, 14, false);
                }
            }
            // Werkbank an der Rückwand (Kollision x −6…1, z −149,1…−148,3, Höhe 1,1 m) mit Lochwand, Schraubstock, Kisten
            var bench = new Vector3(-2.5f, gy, -148.7f);
            mb.For(wood).BevelBox(bench + new Vector3(0, 0.95f, 0), new Vector3(7.0f, 0.08f, 0.8f), 0.02f);
            mb.For(steelD).Box(bench + new Vector3(0, 0.3f, 0), new Vector3(6.9f, 0.04f, 0.7f));
            foreach (float lx in new[] { -3.4f, 0f, 3.4f })
                foreach (float lz in new[] { -0.33f, 0.33f })
                    mb.For(steelD).Box(bench + new Vector3(lx, 0.46f, lz), new Vector3(0.06f, 0.92f, 0.06f));
            mb.For(Paint(new Color(0.55f, 0.6f, 0.62f))).Box(bench + new Vector3(0, 1.95f, -0.37f), new Vector3(5.2f, 1.5f, 0.03f)); // Lochwand
            for (int i = 0; i < 12; i++)
            {
                float tx = -2.3f + i * 0.42f, ty = 1.55f + (i % 3) * 0.33f;
                mb.For(i % 4 == 0 ? Paint(new Color(0.9f, 0.3f, 0.2f)) : steelL).Box(bench + new Vector3(tx, ty, -0.33f), new Vector3(0.06f, 0.3f + (i % 2) * 0.12f, 0.04f));
            }
            mb.For(Paint(new Color(0.85f, 0.25f, 0.18f))).BevelBox(bench + new Vector3(-2.6f, 1.14f, 0.05f), new Vector3(0.7f, 0.3f, 0.36f), 0.03f); // Werkzeugkiste
            mb.For(steelD).BevelBox(bench + new Vector3(1.6f, 1.1f, 0.2f), new Vector3(0.36f, 0.22f, 0.3f), 0.03f);      // Schraubstock
            mb.For(teal).BevelBox(bench + new Vector3(0.2f, 1.2f, 0f), new Vector3(0.9f, 0.4f, 0.5f), 0.04f);            // Ersatzteilkasten
            mb.For(Paint(new Color(0.2f, 0.2f, 0.22f))).BevelBox(bench + new Vector3(2.7f, 0.62f, 0f), new Vector3(0.8f, 0.55f, 0.6f), 0.04f); // Kiste unten
            for (int k = 0; k < 2; k++)
            {
                var lp = bench + new Vector3(-1.2f + k * 2.6f, 2.95f, -0.2f);
                mb.For(steelD).Box(lp + new Vector3(0, 0.05f, -0.1f), new Vector3(0.06f, 0.06f, 0.25f));
                mb.For(warm).Box(lp, new Vector3(0.9f, 0.04f, 0.14f));
            }
            IconSign(mb, new Vector3(-2.5f, gy + 4.3f, z0 + 0.06f), 0, 1.3f, SurfaceLook.Icon.Recycle, new Color(0.18f, 0.72f, 0.68f), false);
            // Regalwand links (Kollision x −7,6…−6,9, z −148,3…−143, Höhe 2,6 m): Stützen, Böden, Kisten und Fässer
            foreach (float uz in new[] { -148.2f, -145.65f, -143.1f })
                foreach (float ux in new[] { -7.52f, -6.98f })
                    mb.For(Paint(new Color(0.2f, 0.4f, 0.75f))).Box(new Vector3(ux, gy + 1.3f, uz), new Vector3(0.07f, 2.6f, 0.07f));
            for (int s = 0; s < 3; s++)
            {
                float y = 0.3f + s * 0.9f;
                mb.For(Paint(new Color(0.95f, 0.55f, 0.18f))).Box(new Vector3(-7.25f, gy + y, -145.65f), new Vector3(0.62f, 0.05f, 5.2f));
                for (int i = 0; i < 7; i++)
                {
                    float z = -147.9f + i * 0.72f;
                    int kind = (i * 3 + s * 5) % 4;
                    if (kind == 0) mb.For(Paint(new Color(0.3f, 0.45f, 0.35f))).Cylinder(new Vector3(-7.25f, gy + y + 0.03f, z), 0.22f, 0.55f, 12);
                    else if (kind == 1) mb.For(wood).BevelBox(new Vector3(-7.25f, gy + y + 0.24f, z), new Vector3(0.5f, 0.42f, 0.55f), 0.03f);
                    else if (kind == 2) mb.For(Paint(new Color(0.85f, 0.85f, 0.8f))).BevelBox(new Vector3(-7.25f, gy + y + 0.17f, z), new Vector3(0.45f, 0.28f, 0.6f), 0.03f);
                }
            }
            // Ladesäule rechts hinten (Kollision x 2,05…2,6, z −147,6…−146,4) mit Anzeige, Kabel und Ladering davor
            var mint = new Color(0.35f, 1f, 0.75f);
            var cp = new Vector3(2.33f, gy, -147.0f);
            mb.For(Paint(new Color(0.9f, 0.88f, 0.82f), 0.5f)).BevelBox(cp + new Vector3(0, 1.0f, 0), new Vector3(0.5f, 2.0f, 1.1f), 0.06f);
            mb.For(teal).BevelBox(cp + new Vector3(0, 2.05f, 0), new Vector3(0.55f, 0.12f, 1.15f), 0.02f);
            mb.For(steelD).BevelBox(cp + new Vector3(-0.26f, 1.45f, 0), new Vector3(0.05f, 0.5f, 0.7f), 0.02f);
            mb.For(Glow(new Color(0.12f, 0.45f, 0.35f), 1.1f)).Box(cp + new Vector3(-0.29f, 1.45f, 0), new Vector3(0.01f, 0.4f, 0.6f));
            mb.For(Glow(mint, 1.6f)).BoxRot(cp + new Vector3(-0.3f, 1.47f, 0), new Vector3(0.005f, 0.24f, 0.07f), new Vector3(25, 0, 0));
            mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Tube(cp + new Vector3(-0.26f, 0.8f, 0.3f), cp + new Vector3(-0.6f, 0.05f, 0.7f), 0.04f, 8);
            mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Tube(cp + new Vector3(-0.6f, 0.05f, 0.7f), cp + new Vector3(-1.3f, 0.05f, 0.4f), 0.04f, 8);
            IconSign(mb, cp + new Vector3(-0.28f, 2.8f, 0), -90f, 0.6f, SurfaceLook.Icon.Bolt, mint, false);
            var ringC = new Vector3(0.6f, gy, -147.0f);
            for (int k = 0; k < 16; k++)
            {
                float ang = (k + 0.5f) / 16f * 360f;
                mb.For(Glow(mint, 0.6f)).BoxRot(ringC + Quaternion.Euler(0, ang, 0) * new Vector3(0, 0.04f, 1.1f), new Vector3(0.36f, 0.02f, 0.04f), new Vector3(0, ang, 0));
            }
            // Wandleuchten innen über dem Tor und an den Seiten
            mb.For(warm).Box(new Vector3(-5.2f, gy + 3.6f, z1 - 0.05f), new Vector3(1.0f, 0.06f, 0.08f));
            mb.For(warm).Box(new Vector3(x0 + 0.05f, gy + 3.6f, -146.0f), new Vector3(0.08f, 0.06f, 1.0f));
        }

        /// <summary>Rolltor des Hangars (eigenes Objekt; Drehpunkt an der Oberkante, rollt zum Öffnen in den Wickelkasten).</summary>
        void BuildHangarDoor(float gy)
        {
            const float fz = -141.5f, w = 5.4f, h = 4.3f;
            var mb = new MultiBuilder { UsePalette = true };
            var slat = Steel(new Color(0.72f, 0.74f, 0.76f));
            var steelD = Steel(new Color(0.24f, 0.25f, 0.27f));
            int n = 15;
            for (int k = 0; k < n; k++)
            {
                float y = -h + (k + 0.5f) * h / n;
                mb.For(k == 9 ? WindowMat(0) : slat).Box(new Vector3(0, y, 0.03f), new Vector3(w, h / n - 0.02f, 0.05f));
                mb.For(steelD).Box(new Vector3(0, y + h / n * 0.5f - 0.01f, 0.06f), new Vector3(w, 0.025f, 0.02f));
            }
            mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Box(new Vector3(0, -h + 0.04f, 0.03f), new Vector3(w, 0.08f, 0.09f));
            HazardBand(mb, new Vector3(0, -h + 0.22f, 0.07f), 0, w, 0.16f, 0.02f);
            mb.For(steelD).Box(new Vector3(-1.6f, -h + 0.9f, 0.08f), new Vector3(0.3f, 0.06f, 0.05f)); // Griff
            var go = mb.Build("HangarTor", Root, true);
            go.transform.localPosition = new Vector3(0, gy + h, fz + 0.02f);
            hangarDoor = go.transform;
            hangarOpen = 0f;
            hangarDoor.localScale = Vector3.one;
            var lg = new GameObject("HangarLicht");
            lg.transform.SetParent(Root, false);
            lg.transform.localPosition = new Vector3(-2.5f, gy + 5.0f, -145.5f);
            hangarLight = lg.AddComponent<Light>();
            hangarLight.type = LightType.Point; hangarLight.range = 11f; hangarLight.color = new Color(1f, 0.8f, 0.58f); hangarLight.intensity = 1.6f;
            hangarLight.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ Transportschiff
        /// <summary>Lage der Hub- und Haupttriebwerke im Schiffsrahmen (x gespiegelt; für die Anflugszene).</summary>
        public static readonly Vector3 ShipLiftEngine = new Vector3(5.6f, 2.05f, -1.5f), ShipMainEngine = new Vector3(3.45f, 3.4f, -6.6f);
        /// <summary>Laderaum im Schiffsrahmen: Heck z −5,6, Bugschott z 4,0, halbe Breite 2,7, Boden 0,7 über dem Landeplatz, Decke 3,8.</summary>
        const float SRear = -5.6f, SFront = 4.0f, SHalf = 2.7f, SFloor = 0.7f, SCeil = 3.8f, RampLen = 4.09f;
        const float RampOpenAngle = -12f, RampClosedAngle = 88f;

        /// <summary>Drehung der Heckrampe (lokal zum Schiffsobjekt): open 0 = zu (senkrecht), 1 = abgesenkt auf den Boden.</summary>
        public static Quaternion ShipRampRotation(float shipYaw, float open)
        {
            return Quaternion.Euler(0, shipYaw, 0) * Quaternion.Euler(Mathf.Lerp(RampClosedAngle, RampOpenAngle, M.Smooth(Mathf.Clamp01(open))), 0, 0);
        }

        /// <summary>Kegelstumpf mit rechteckigem Querschnitt entlang z (für Bug und Nase), Flächen nach außen.</summary>
        static void Frustum(MeshBuilder b, float z0, float hx0, float yb0, float yt0, float z1, float hx1, float yb1, float yt1, bool cap0, bool cap1)
        {
            Vector3 a0 = new Vector3(-hx0, yb0, z0), b0 = new Vector3(hx0, yb0, z0), c0 = new Vector3(hx0, yt0, z0), d0 = new Vector3(-hx0, yt0, z0);
            Vector3 a1 = new Vector3(-hx1, yb1, z1), b1 = new Vector3(hx1, yb1, z1), c1 = new Vector3(hx1, yt1, z1), d1 = new Vector3(-hx1, yt1, z1);
            b.Face(a0, b0, b1, a1, Vector3.down);
            b.Face(d0, c0, c1, d1, Vector3.up);
            b.Face(a0, d0, d1, a1, Vector3.left);
            b.Face(b0, c0, c1, b1, Vector3.right);
            if (cap0) b.Face(a0, b0, c0, d0, Vector3.back);
            if (cap1) b.Face(a1, b1, c1, d1, Vector3.forward);
        }

        /// <summary>
        /// Kastenförmiger Frachter, Nase nach +X (Layout.Base.ShipYaw): begehbarer Laderaum (Boden 0,7 m über dem Landeplatz,
        /// Kollision WorldGen.BuildShipHold) mit Spanten, Deckenleuchten, Zurrschienen und verzurrten Containern, Bugschott mit
        /// halb offenem Durchgang zum beleuchteten Cockpit (Sitze, Konsolen, Frontscheibe), abgeschrägter Bug mit Kanzel,
        /// Stummelflügel mit Hubtriebwerken, zwei Haupttriebwerke an den Schultern, vier Landebeine und eine Heckrampe
        /// (eigenes Objekt „Rampe“, senkt sich nachts/bei Sturm, sobald MIKO sich nähert). Alle Wände sind Scheiben, damit man
        /// sie auch von innen sieht.
        /// </summary>
        void BuildShip(Vector3 at)
        {
            var mb = new MultiBuilder { UsePalette = true };
            var hull = Mats.Surface(SurfKind.Paint, new Color(0.86f, 0.87f, 0.9f), 0.6f, true);
            var hullDark = Steel(new Color(0.34f, 0.36f, 0.4f));
            var dark = Paint(new Color(0.16f, 0.17f, 0.2f));
            var accent = Paint(new Color(1f, 0.55f, 0.18f), 0.55f);
            var teal = Paint(new Color(0.18f, 0.72f, 0.68f), 0.55f);
            var lining = Mats.Surface(SurfKind.Cladding, new Color(0.7f, 0.72f, 0.74f), 0.4f);
            var glass = Mats.Get(Mats.Emissive, new Color(0.08f, 0.16f, 0.24f), new Color(0.06f, 0.22f, 0.32f), 0.95f);
            var engineGlow = Glow(new Color(0.4f, 0.85f, 1f), 2.2f);
            var lamp = Glow(new Color(1f, 0.9f, 0.75f), 2.4f);
            float yawDeg = Layout.Base.ShipYaw;
            shipYawQ = Quaternion.Euler(0, yawDeg, 0);
            mb.M = Matrix4x4.TRS(at, shipYawQ, Vector3.one);
            mb.GroundY = at.y - 0.2f;
            float midZ = (SRear + SFront + 0.4f) * 0.5f, len = SFront + 0.4f - SRear;
            // ---- Rumpf: Bodenwanne, Seitenwände, Schultern, Dach (Scheiben)
            mb.For(hullDark).Box(new Vector3(0, 0.5f, midZ), new Vector3(6.2f, 0.4f, len));
            mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.14f, 0.15f, 0.17f), 0.3f)).Box(new Vector3(0, 0.27f, midZ), new Vector3(5.8f, 0.06f, len - 0.4f));
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(hull).BevelBox(new Vector3(sx * 2.9f, 2.15f, midZ), new Vector3(0.4f, 3.3f, len), 0.08f);
                mb.For(hull).BevelBoxRot(new Vector3(sx * 2.65f, 4.2f, midZ), new Vector3(1.2f, 0.3f, len), new Vector3(0, 0, -sx * 42f), 0.06f);
                // Akzentstreifen, Bullaugen, Lüftungsgitter, Piktogramm
                mb.For(accent).Box(new Vector3(sx * 3.12f, 3.1f, midZ), new Vector3(0.04f, 0.3f, len - 0.6f));
                mb.For(teal).Box(new Vector3(sx * 3.12f, 1.0f, midZ), new Vector3(0.04f, 0.14f, len - 0.6f));
                for (int k = 0; k < 4; k++)
                {
                    var wp = new Vector3(sx * 3.12f, 2.4f, -3.9f + k * 1.9f);
                    mb.For(hullDark).CylinderX(wp, 0.22f, 0.06f, 12);
                    mb.For(Glow(new Color(1f, 0.8f, 0.55f), 0.9f)).CylinderX(wp + new Vector3(sx * 0.02f, 0, 0), 0.16f, 0.05f, 12);
                }
                for (int k = 0; k < 6; k++) mb.For(dark).Box(new Vector3(sx * 3.12f, 1.6f, 2.4f + k * 0.2f), new Vector3(0.03f, 0.35f, 0.08f));
                IconSign(mb, new Vector3(sx * 3.16f, 2.4f, -4.9f), sx * 90f, 0.7f, SurfaceLook.Icon.Ship, new Color(0.3f, 0.75f, 1f), false);
            }
            mb.For(hull).BevelBox(new Vector3(0, 4.45f, midZ), new Vector3(4.6f, 0.3f, len), 0.06f);
            // Dachaufbauten: Wärmetauscher, Antenne, Rundumleuchte
            mb.For(hullDark).BevelBox(new Vector3(0, 4.65f, -2.4f), new Vector3(1.8f, 0.12f, 2.6f), 0.03f);
            for (int k = 0; k < 8; k++) mb.For(dark).Box(new Vector3(0, 4.85f, -3.5f + k * 0.32f), new Vector3(1.6f, 0.3f, 0.05f));
            mb.For(hullDark).Cylinder(new Vector3(0.9f, 4.6f, 1.6f), 0.05f, 1.6f, 6);
            for (int k = 0; k < 3; k++) mb.For(hullDark).Box(new Vector3(0.9f, 5.35f + k * 0.3f, 1.6f), new Vector3(0.5f - k * 0.12f, 0.02f, 0.02f));
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(0.9f, 6.25f, 1.6f), 0.1f, 6, 4);
            // ---- Heckrahmen um die Ladeluke, Scharniere
            mb.For(hullDark).BevelBox(new Vector3(0, 4.2f, SRear - 0.1f), new Vector3(6.3f, 0.85f, 0.3f), 0.05f);
            mb.For(accent).Box(new Vector3(0, 3.95f, SRear - 0.26f), new Vector3(5.4f, 0.12f, 0.02f));
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(hullDark).BevelBox(new Vector3(sx * 2.95f, 2.2f, SRear - 0.1f), new Vector3(0.5f, 3.6f, 0.3f), 0.05f);
                mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(sx * 2.72f, 2.2f, SRear - 0.26f), new Vector3(0.05f, 3.2f, 0.02f));
                mb.For(Glow(new Color(1f, 0.6f, 0.2f), 2f)).Box(new Vector3(sx * 2.95f, 3.6f, SRear - 0.27f), new Vector3(0.18f, 0.18f, 0.02f)); // Ladeleuchte
                mb.For(hullDark).CylinderX(new Vector3(sx * 2.15f, SFloor - 0.02f, SRear - 0.05f), 0.13f, 0.6f, 10);
            }
            // ---- Laderaum innen: Verkleidung, Spanten, Boden, Zurrschienen, Decke mit Leuchten
            float inMid = (SRear + SFront) * 0.5f, inLen = SFront - SRear;
            mb.For(lining).Box(new Vector3(0, SCeil + 0.05f, inMid), new Vector3(SHalf * 2f, 0.1f, inLen));
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(lining).Box(new Vector3(sx * (SHalf - 0.02f), 2.25f, inMid), new Vector3(0.04f, 3.1f, inLen));
                mb.For(teal).Box(new Vector3(sx * (SHalf - 0.05f), 1.25f, inMid), new Vector3(0.03f, 0.18f, inLen));
                mb.For(Rubber(new Color(0.15f, 0.15f, 0.16f))).Box(new Vector3(sx * (SHalf - 0.04f), SFloor + 0.15f, inMid), new Vector3(0.05f, 0.3f, inLen));
                for (int k = 0; k < 3; k++) mb.For(lamp).Box(new Vector3(sx * 1.6f, SCeil - 0.03f, -4.2f + k * 2.7f), new Vector3(0.22f, 0.04f, 1.3f));
                mb.For(hullDark).Box(new Vector3(sx * 1.4f, SFloor + 0.04f, inMid), new Vector3(0.08f, 0.04f, inLen - 0.4f)); // Zurrschiene
            }
            for (float z = SRear + 0.4f; z < SFront - 1.5f; z += 1.6f)
            {
                foreach (var sx in new[] { -1f, 1f }) mb.For(hullDark).Box(new Vector3(sx * (SHalf - 0.07f), 2.25f, z), new Vector3(0.14f, 3.1f, 0.16f));
                mb.For(hullDark).Box(new Vector3(0, SCeil - 0.07f, z), new Vector3(SHalf * 2f - 0.2f, 0.14f, 0.16f));
            }
            mb.For(Mats.Surface(SurfKind.Tiles, new Color(0.36f, 0.38f, 0.4f), 0.5f, true)).Box(new Vector3(0, SFloor + 0.01f, inMid), new Vector3(SHalf * 2f, 0.02f, inLen));
            HazardBand(mb, new Vector3(0, SFloor + 0.02f, SRear + 0.2f), 0, SHalf * 2f, 0.02f, 0.3f);
            // Container vorn in den Ecken (Kollision z 2,6…4,0, |x| 1,5…2,7, Höhe 1,6 m), verzurrt, mit Kisten obenauf
            foreach (var sx in new[] { -1f, 1f })
            {
                var cc = new Vector3(sx * 2.1f, SFloor + 0.78f, SFront - 0.7f);
                var cm = sx < 0 ? accent : teal;
                mb.For(cm).BevelBox(cc, new Vector3(1.15f, 1.55f, 1.35f), 0.04f);
                for (int k = 0; k < 5; k++) mb.For(hullDark).Box(cc + new Vector3(-sx * 0.585f, 0, -0.55f + k * 0.275f), new Vector3(0.02f, 1.45f, 0.06f));
                mb.For(Paint(new Color(0.95f, 0.95f, 0.92f))).Box(cc + new Vector3(-sx * 0.59f, 0.35f, 0), new Vector3(0.01f, 0.25f, 0.6f));
                mb.For(Rubber(new Color(0.9f, 0.55f, 0.12f))).Box(cc + new Vector3(0, 0.78f, 0), new Vector3(1.2f, 0.02f, 0.06f));
                mb.For(Rubber(new Color(0.9f, 0.55f, 0.12f))).Box(cc + new Vector3(-sx * 0.6f, 0, 0), new Vector3(0.02f, 1.56f, 0.06f));
                mb.For(Mats.Surface(SurfKind.Wood, new Color(0.55f, 0.42f, 0.28f), 0.3f)).BevelBox(cc + new Vector3(0.1f * sx, 1.1f, 0.1f), new Vector3(0.7f, 0.6f, 0.7f), 0.03f);
            }
            // Bugschott mit Durchgang (x ±0,65, Höhe 2,2 m) und halb aufgeschobener Tür
            float bz = SFront + 0.2f;
            mb.For(lining).Box(new Vector3(-(SHalf + 0.65f) * 0.5f, 2.25f, bz), new Vector3(SHalf - 0.65f, 3.1f, 0.4f));
            mb.For(lining).Box(new Vector3((SHalf + 0.65f) * 0.5f, 2.25f, bz), new Vector3(SHalf - 0.65f, 3.1f, 0.4f));
            mb.For(lining).Box(new Vector3(0, 3.35f, bz), new Vector3(1.3f, 0.9f, 0.4f));
            foreach (var sx in new[] { -1f, 1f }) mb.For(hullDark).Box(new Vector3(sx * 0.7f, 1.8f, SFront - 0.02f), new Vector3(0.1f, 2.2f, 0.06f));
            mb.For(hullDark).Box(new Vector3(0, 2.92f, SFront - 0.02f), new Vector3(1.5f, 0.1f, 0.06f));
            mb.For(Paint(new Color(0.3f, 0.34f, 0.38f))).Box(new Vector3(0.62f, 1.8f, SFront + 0.2f), new Vector3(0.9f, 2.18f, 0.06f));
            mb.For(Glow(new Color(0.3f, 1f, 0.6f), 1.4f)).Box(new Vector3(0.9f, 1.95f, SFront - 0.03f), new Vector3(0.08f, 0.14f, 0.02f));
            IconSign(mb, new Vector3(-1.7f, 2.7f, SFront - 0.03f), 180f, 0.6f, SurfaceLook.Icon.Ship, new Color(0.3f, 0.75f, 1f), false);
            // Cockpit hinter dem Schott (nur durch den Durchgang sichtbar): Boden, Seitenwände, Decke, Sitze, Konsolen, Frontscheibe
            float ck0 = SFront + 0.4f, ck1 = 7.0f;
            mb.For(dark).Box(new Vector3(0, SFloor + 0.1f, (ck0 + ck1) * 0.5f), new Vector3(3.0f, 0.2f, ck1 - ck0));  // Cockpitboden
            mb.For(dark).Box(new Vector3(0, SFloor + 0.1f, SFront + 0.2f), new Vector3(1.28f, 0.2f, 0.4f));          // Stufe im Durchgang
            mb.For(lining).Box(new Vector3(0, 3.3f, (ck0 + ck1) * 0.5f - 0.2f), new Vector3(3.0f, 0.08f, ck1 - ck0 - 0.4f));
            foreach (var sx in new[] { -1f, 1f })
            {
                mb.For(lining).Box(new Vector3(sx * 1.5f, 2.1f, (ck0 + ck1) * 0.5f), new Vector3(0.06f, 2.4f, ck1 - ck0));
                var seat = new Vector3(sx * 0.65f, SFloor + 0.2f, 5.4f);
                mb.For(dark).Cylinder(seat, 0.08f, 0.45f, 8);
                mb.For(Paint(new Color(0.85f, 0.4f, 0.15f))).BevelBox(seat + new Vector3(0, 0.55f, 0), new Vector3(0.55f, 0.12f, 0.55f), 0.04f);
                mb.For(Paint(new Color(0.85f, 0.4f, 0.15f))).BevelBoxRot(seat + new Vector3(0, 0.95f, -0.25f), new Vector3(0.55f, 0.75f, 0.1f), new Vector3(-10, 0, 0), 0.04f);
            }
            var console = new Vector3(0, SFloor + 0.2f, 6.55f);
            mb.For(hullDark).BevelBoxRot(console + new Vector3(0, 0.55f, 0), new Vector3(2.8f, 0.9f, 0.6f), new Vector3(-20, 0, 0), 0.05f);
            for (int k = 0; k < 3; k++)
                mb.For(Glow(k == 1 ? new Color(0.3f, 0.9f, 1f) : new Color(0.35f, 1f, 0.6f), 1.5f)).BoxRot(console + new Vector3(-0.9f + k * 0.9f, 1.02f, 0.02f), new Vector3(0.7f, 0.02f, 0.4f), new Vector3(-20, 0, 0));
            mb.For(glass).BoxRot(new Vector3(0, 2.55f, 6.75f), new Vector3(2.9f, 0.04f, 1.6f), new Vector3(55, 0, 0)); // Frontscheibe innen
            // ---- Bug mit Kanzel (Kegelstumpf-Segmente), Scheiben und Rahmen
            var nb = mb.For(hull);
            Frustum(nb, SFront + 0.4f, 3.1f, 0.3f, 4.6f, 7.0f, 2.3f, 0.7f, 3.5f, false, false);
            Frustum(nb, 7.0f, 2.3f, 0.7f, 3.5f, 8.6f, 1.1f, 1.25f, 2.6f, false, true);
            mb.For(glass).BoxRot(new Vector3(0, 3.91f, 6.1f), new Vector3(3.2f, 0.04f, 1.5f), new Vector3(22.9f, 0, 0));
            mb.For(glass).BoxRot(new Vector3(0, 3.22f, 7.55f), new Vector3(2.4f, 0.04f, 1.0f), new Vector3(29.4f, 0, 0));
            foreach (var sx in new[] { -0.6f, 0.6f }) mb.For(hullDark).BoxRot(new Vector3(sx, 3.94f, 6.1f), new Vector3(0.07f, 0.06f, 1.55f), new Vector3(22.9f, 0, 0));
            foreach (var sx in new[] { -1f, 1f })
            {
                var sw = new Vector3(sx * 2.68f, 2.9f, 6.0f);
                mb.For(glass).BoxRot(sw, new Vector3(0.04f, 0.7f, 1.2f), new Vector3(0, sx * 17f, 0));
                mb.For(Glow(sx < 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f), 3f)).Sphere(new Vector3(sx * 2.1f, 1.6f, 7.4f), 0.1f, 6, 4);
            }
            mb.For(accent).Box(new Vector3(0, 1.9f, 8.62f), new Vector3(1.4f, 0.3f, 0.04f));
            // ---- Stummelflügel mit Hubtriebwerken, Haupttriebwerke an den Schultern
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(hull).BevelBoxRot(new Vector3(s * 4.7f, 2.55f, -1.6f), new Vector3(3.2f, 0.26f, 3.4f), new Vector3(0, 0, s * 6f), 0.08f);
                mb.For(accent).BoxRot(new Vector3(s * 4.9f, 2.72f, -0.1f), new Vector3(2.2f, 0.06f, 0.4f), new Vector3(0, 0, s * 6f));
                var pod = new Vector3(s * ShipLiftEngine.x, ShipLiftEngine.y, ShipLiftEngine.z);
                mb.For(hullDark).Cylinder(pod, 0.72f, 1.0f, 18);
                mb.For(accent).Cylinder(pod + Vector3.up * 0.75f, 0.74f, 0.15f, 18, false);
                mb.For(dark).Cylinder(pod + Vector3.down * 0.05f, 0.55f, 0.08f, 18);
                mb.For(engineGlow).Disc(pod + Vector3.down * 0.06f, 0.4f, 18, false);
                mb.For(Glow(s < 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f), 3f)).Sphere(pod + new Vector3(s * 0.78f, 0.5f, 0), 0.1f, 6, 4);
                var n = new Vector3(s * ShipMainEngine.x, ShipMainEngine.y, 0);
                mb.For(hull).CylinderZ(new Vector3(n.x, n.y, -3.6f), 0.72f, 4.6f, 20);
                mb.For(accent).CylinderZ(new Vector3(n.x, n.y, -1.4f), 0.74f, 0.3f, 20);
                mb.For(dark).CylinderZ(new Vector3(n.x, n.y, -1.25f), 0.5f, 0.04f, 16);
                mb.For(hullDark).CylinderZ(new Vector3(n.x, n.y, -6.1f), 0.66f, 0.4f, 20);
                mb.For(engineGlow).CylinderZ(new Vector3(n.x, n.y, -6.32f), 0.44f, 0.04f, 20);
                mb.For(Glow(new Color(0.4f, 0.85f, 1f), 1.2f)).TorusRot(new Vector3(n.x, n.y, -6.3f), new Vector3(90, 0, 0), 0.55f, 0.04f, 20, 4);
                for (int k = 0; k < 10; k++)
                {
                    float ang = k * 36f;
                    mb.For(hullDark).BoxRot(new Vector3(n.x, n.y, -6.22f) + Quaternion.Euler(0, 0, ang) * new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.05f, 0.22f), new Vector3(0, 0, ang));
                }
            }
            // ---- Landebeine (Kollision je 0,9 m um die Füße bei x ±3,8, z 3,0 / −4,4)
            foreach (var lz in new[] { 3.0f, -4.4f })
                foreach (var sx in new[] { -1f, 1f })
                {
                    var foot = new Vector3(sx * 3.8f, 0.0f, lz);
                    var hip = new Vector3(sx * 3.1f, 1.4f, lz);
                    var knee = new Vector3(sx * 3.75f, 0.9f, lz);
                    mb.For(hullDark).Beam(hip, knee, 0.24f);
                    mb.For(acMat).Tube(knee, foot + Vector3.up * 0.18f, 0.09f, 10);
                    mb.For(Steel(new Color(0.8f, 0.82f, 0.85f))).Tube(hip + new Vector3(0, 0.3f, 0.2f), knee + new Vector3(0, 0.15f, 0.2f), 0.045f, 8);
                    mb.For(dark).Cylinder(foot, 0.42f, 0.18f, 16, true, 0.3f);
                    mb.For(Rubber(new Color(0.1f, 0.1f, 0.1f))).Cylinder(foot + Vector3.down * 0.03f, 0.44f, 0.05f, 16);
                    mb.For(Paint(new Color(0.95f, 0.75f, 0.15f))).BevelBox(knee, new Vector3(0.32f, 0.3f, 0.32f), 0.04f);
                }
            var go = mb.Build("TransportShip", Root, true);
            // ---- Heckrampe (eigenes Objekt unter dem Schiff; Drehpunkt am Scharnier)
            var rb = new MultiBuilder { UsePalette = true };
            rb.For(Mats.Surface(SurfKind.Tiles, new Color(0.4f, 0.42f, 0.45f), 0.5f, true)).Box(new Vector3(0, -0.08f, -RampLen * 0.5f), new Vector3(5.0f, 0.16f, RampLen));
            rb.For(hullDark).Box(new Vector3(0, -0.2f, -RampLen * 0.5f), new Vector3(4.6f, 0.08f, RampLen - 0.3f));
            foreach (var sx in new[] { -1f, 1f }) rb.For(accent).Box(new Vector3(sx * 2.45f, 0.02f, -RampLen * 0.5f), new Vector3(0.1f, 0.12f, RampLen));
            for (int k = 0; k < 6; k++) rb.For(Paint(new Color(0.95f, 0.8f, 0.2f))).Box(new Vector3(0, 0.005f, -0.4f - k * 0.62f), new Vector3(3.6f, 0.02f, 0.12f));
            rb.For(hullDark).CylinderX(new Vector3(0, -0.06f, 0), 0.11f, 4.4f, 10);
            rb.For(dark).Box(new Vector3(0, -0.04f, -RampLen + 0.08f), new Vector3(5.0f, 0.1f, 0.16f)); // Auflagekante
            var ramp = rb.Build("Rampe", go.transform, true);
            ramp.transform.localPosition = at + shipYawQ * new Vector3(0, SFloor, SRear);
            rampOpen = 0f;
            ramp.transform.localRotation = ShipRampRotation(yawDeg, rampOpen);
            shipRamp = ramp.transform;
            var lg = new GameObject("SchiffLicht");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = at + shipYawQ * new Vector3(0, 3.2f, -0.8f);
            shipLight = lg.AddComponent<Light>();
            shipLight.type = LightType.Point; shipLight.range = 9f; shipLight.color = new Color(1f, 0.9f, 0.78f); shipLight.intensity = 1.5f;
            shipLight.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ Tor und Rampe bewegen
        /// <summary>
        /// Rolltor und Heckrampe: nachts, bei Sturmwarnung und im Sturm öffnen sie, sobald jemand (MIKO oder ein Mitspieler)
        /// auf 13 m herankommt; tagsüber erst direkt davor (6 m). Solange jemand drinnen ist, bleiben sie offen. Innenlicht
        /// nur in Kameranähe (Punktlichter kosten).
        /// </summary>
        void AnimateShelters()
        {
            var w = World;
            var b = Layout != null ? Layout.Base : null;
            if (w == null || b == null || b.Hangar == null || b.Ship == null || !w.Planets.ContainsKey(Planet)) return;
            var ps = w.Planet(Planet);
            bool danger = Rules.IsNight(w, Planet) || ps.StormActive || ps.StormWarn;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float near = danger ? 13f : 6f;
            var cam = Camera.main;
            if (hangarDoor != null)
            {
                float prev = hangarOpen;
                hangarOpen = Mathf.MoveTowards(hangarOpen, WantOpen(w, b.Hangar, near) ? 1f : 0f, dt * 0.85f);
                hangarDoor.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.04f, M.Smooth(hangarOpen)), 1f);
                var dp = new Vector3(b.Hangar.Door.Cx, b.Hangar.Door.Y0 + 3f, b.Hangar.Door.Cz);
                hangarMoving = MoveSound("hangar_door", hangarOpen != prev, hangarMoving, dp);
            }
            if (shipRamp != null)
            {
                float prev = rampOpen;
                rampOpen = Mathf.MoveTowards(rampOpen, WantOpen(w, b.Ship, near) ? 1f : 0f, dt * 0.7f);
                shipRamp.localRotation = ShipRampRotation(b.ShipYaw, rampOpen);
                var rp = new Vector3(b.Ship.Door.Cx, b.ShipFloorY, b.Ship.Door.Cz);
                rampMoving = MoveSound("ship_ramp", rampOpen != prev, rampMoving, rp);
            }
            if (hangarLight != null) hangarLight.enabled = cam != null && (cam.transform.position - hangarLight.transform.position).sqrMagnitude < 45f * 45f;
            if (shipLight != null) shipLight.enabled = cam != null && (cam.transform.position - shipLight.transform.position).sqrMagnitude < 40f * 40f && (rampOpen > 0.05f || b.Ship.Contains(new V3(cam.transform.position.x, cam.transform.position.y - 1f, cam.transform.position.z)));
        }

        static bool MoveSound(string key, bool moving, bool wasMoving, Vector3 at)
        {
            AudioManager.Loop(key, "machine_loop", moving, at, 0.35f, 0.7f);
            if (wasMoving && !moving) AudioManager.Play("metal", at, 0.3f, 0.6f);
            return moving;
        }

        /// <summary>Jemand (online, nicht abgeschaltet) drinnen oder nahe am Eingang?</summary>
        static bool WantOpen(WorldState w, ShelterRoom r, float near)
        {
            var app = GameApp.I;
            if (app != null && app.InGame && PlayerController.I != null)
            {
                var rp = PlayerController.I.RenderPos;
                if (NearRoom(r, new V3(rp.x, rp.y, rp.z), near)) return true;
            }
            foreach (var p in w.Players.Values)
                if (p.Online && p.TowTimer <= 0 && NearRoom(r, p.Pos, near)) return true;
            return false;
        }

        static bool NearRoom(ShelterRoom r, V3 p, float near)
        {
            if (r.Contains(p, 0.3f)) return true;
            float dx = p.x - r.Door.Cx, dz = p.z - r.Door.Cz;
            return dx * dx + dz * dz < near * near;
        }
    }
}
