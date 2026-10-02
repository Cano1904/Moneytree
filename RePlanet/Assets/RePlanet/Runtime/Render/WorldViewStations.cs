using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Eine eigene Roboterstation je Planet – gleiche Spielfläche (Kollision, Hangar, Stationen wie WorldGen.BuildBase),
    /// eigene Architektur und Materialien:
    /// <list type="bullet">
    /// <item>TERRA – umgebautes altes Stadtdepot: Backstein mit Rollschichten, Sheddach mit Oberlichtern, Rolltor,
    /// Leuchtröhren-Schriftzug „MIKO“ (flackert nachts), Feuerleiter.</item>
    /// <item>PYRA – Gießerei-Bunker, halb in den roten Fels gegraben: Sichtbeton, Felsflanke und Fels auf dem Dach,
    /// schweres Stahl-Hubtor, Schornsteine mit Glut und Rauch, Rohrleitungen, glühende Abluftgitter, Erzbunker.</item>
    /// <item>PELAGIA – Pfahlbau-Station mit Bootshaus: Holzverschalung auf Stelzen, Bohlensteg vor dem Tor, Hafenbecken
    /// mit Boot, Davit-Kran, Bojen, Rettungsring, Leuchtfeuer mit drehendem Strahl, Holz-Rolltor.</item>
    /// <item>NIVALIS – isolierte Forschungskuppel: Paneelwände, Kuppeldach mit Schnee, rundes Schleusentor (zwei
    /// Schiebehälften), Antennenschüsseln, Radom, Heizstrahler, Kryotanks.</item>
    /// </list>
    /// Feines (Schrauben, Nieten, Kleinteile) liegt in „BaseDetail“ und wird ab 75 m ausgeblendet. Lichter leuchten nachts,
    /// die Kabinenlampen im Hangar dimmen, solange jemand darin schläft.
    /// </summary>
    public partial class WorldView
    {
        /// <summary>Stil der Station: 0 TERRA, 1 PYRA, 2 PELAGIA, 3 NIVALIS.</summary>
        int stationStyle;
        Color baseLampCol = new Color(1f, 0.8f, 0.55f);
        Material neonMat, ventMat, beaconMat, cabinMat, heatMat;
        Color cabinCol = new Color(1f, 0.86f, 0.62f);
        Transform lighthouseBeam, hangarDoorR, baseDetail;
        readonly List<Vector3> chimneyTops = new List<Vector3>();
        float smokeTimer, cabinDim;

        static int StyleOf(string planet) { return planet == "pyra" ? 1 : planet == "pelagia" ? 2 : planet == "nivalis" ? 3 : 0; }

        void InitStationStyle()
        {
            stationStyle = StyleOf(Planet);
            baseLampCol = stationStyle == 1 ? new Color(1f, 0.62f, 0.3f) : stationStyle == 3 ? new Color(0.75f, 0.88f, 1f) : stationStyle == 2 ? new Color(1f, 0.9f, 0.72f) : new Color(1f, 0.8f, 0.55f);
            cabinCol = stationStyle == 1 ? new Color(1f, 0.7f, 0.4f) : stationStyle == 3 ? new Color(0.8f, 0.92f, 1f) : new Color(1f, 0.86f, 0.62f);
            baseLampMat = null;
            cabinMat = Mats.Unique(Mats.Emissive, cabinCol);
            Mats.SetEmission(cabinMat, cabinCol * 2.2f);
            neonMat = ventMat = beaconMat = heatMat = null;
            lighthouseBeam = null; hangarDoorR = null; baseDetail = null;
            chimneyTops.Clear();
            cabinDim = 0f;
        }

        static Material Brick(Color c) { return Mats.Surface(SurfKind.Brick, Q(c)); }
        static Material Wood(Color c, float g = 0.3f) { return Mats.Surface(SurfKind.Wood, Q(c), g); }
        static Material Clad(Color c, float g = 0.4f) { return Mats.Surface(SurfKind.Cladding, Q(c), g); }
        static Material Stone(Color c) { return Mats.Surface(SurfKind.Stone, Q(c)); }

        // ================================================================== Gemeinsam
        /// <summary>Wände der Halle (Kollision wie WorldGen.BuildHangar) als Scheiben, damit man sie auch von innen sieht.</summary>
        void HallShell(MultiBuilder mb, float gy, Material wall, float top)
        {
            const float fz = -141.5f;
            float h = top, cy = gy + h * 0.5f;
            mb.For(wall).BevelBox(new Vector3(-2.2f, cy, -149.3f), new Vector3(11.6f, h, 0.4f), 0.08f);
            mb.For(wall).BevelBox(new Vector3(-7.8f, cy, -145.5f), new Vector3(0.4f, h, 8f), 0.08f);
            mb.For(wall).BevelBox(new Vector3(3.1f, cy, -145.5f), new Vector3(1.0f, h, 8f), 0.08f);
            mb.For(wall).BevelBox(new Vector3(-5.1f, cy, fz - 0.2f), new Vector3(5.0f, h, 0.4f), 0.06f);
            mb.For(wall).Box(new Vector3(0f, gy + 4.3f + (h - 4.3f) * 0.5f, fz - 0.2f), new Vector3(5.2f, h - 4.3f, 0.4f));
        }

        /// <summary>Waagerechte Bänder (Fugen, Rollschichten, Bretterstöße) auf der Außenseite der Hallenwände.</summary>
        void WallBands(MultiBuilder mb, float gy, Material m, float y0, float y1, float step, float depth, float thick, bool front = true)
        {
            const float fz = -141.5f;
            for (float y = y0; y < y1; y += step)
            {
                mb.For(m).Box(new Vector3(-2.2f, gy + y, -149.5f - depth * 0.5f), new Vector3(11.7f, thick, depth));
                mb.For(m).Box(new Vector3(-8f - depth * 0.5f, gy + y, -145.5f), new Vector3(depth, thick, 8.1f));
                if (front)
                {
                    mb.For(m).Box(new Vector3(-5.2f, gy + y, fz + depth * 0.5f), new Vector3(5.6f, thick, depth));
                    if (y > 4.35f) mb.For(m).Box(new Vector3(0f, gy + y, fz + depth * 0.5f), new Vector3(5.2f, thick, depth));
                }
            }
        }

        /// <summary>Unterplatte unter einer Station je Planet (Bohlen, Gitterrost, beheizte Platte) – rein flach, befahrbar.</summary>
        void StationPad(MultiBuilder mb, MultiBuilder det, Vector3 sp)
        {
            var c = new Vector3(sp.x, sp.y, sp.z - 0.7f);
            switch (stationStyle)
            {
                case 1:
                    mb.For(Steel(new Color(0.26f, 0.22f, 0.2f))).Box(c + new Vector3(0, 0.012f, 0), new Vector3(2.4f, 0.024f, 2.6f));
                    for (int k = 0; k < 9; k++) det.For(Steel(new Color(0.16f, 0.13f, 0.12f))).Box(c + new Vector3(-1.1f + k * 0.275f, 0.026f, 0), new Vector3(0.03f, 0.006f, 2.5f));
                    break;
                case 2:
                    for (int k = 0; k < 9; k++) mb.For(Wood(k % 3 == 0 ? new Color(0.5f, 0.38f, 0.27f) : new Color(0.56f, 0.43f, 0.3f))).Box(c + new Vector3(0, 0.014f, -1.2f + k * 0.3f), new Vector3(2.4f, 0.028f, 0.27f));
                    break;
                case 3:
                    mb.For(Conc(new Color(0.55f, 0.6f, 0.66f))).Box(c + new Vector3(0, 0.012f, 0), new Vector3(2.4f, 0.024f, 2.6f));
                    det.For(Paint(new Color(0.95f, 0.45f, 0.15f))).Box(c + new Vector3(0, 0.027f, 1.27f), new Vector3(2.4f, 0.006f, 0.06f));
                    break;
            }
        }

        // ================================================================== TERRA: altes Stadtdepot
        void TerraDepotExtras(MultiBuilder mb, MultiBuilder det, float gy)
        {
            const float fz = -141.5f;
            var brickD = Brick(new Color(0.48f, 0.24f, 0.19f));
            var stoneL = Stone(new Color(0.78f, 0.74f, 0.66f));
            var steelD = Steel(new Color(0.22f, 0.23f, 0.25f));
            // Rollschichten (vorstehende Backsteinbänder) und Sohlbank-Gesims
            WallBands(mb, gy, brickD, 1.0f, 6.9f, 1.3f, 0.05f, 0.12f);
            foreach (var x in new[] { -7.9f, -5.2f, -2.75f, 2.75f })
                det.For(brickD).Box(new Vector3(x, gy + 3.5f, fz + 0.09f), new Vector3(0.42f, 6.9f, 0.06f));
            mb.For(stoneL).BevelBox(new Vector3(-2.2f, gy + 7.05f, fz + 0.12f), new Vector3(11.8f, 0.18f, 0.24f), 0.03f);
            // Sheddach (drei Zähne mit Oberlichtern zur Nordseite) über der Attika
            var roofM = Steel(new Color(0.36f, 0.33f, 0.31f));
            var glass = WindowMat(0);
            for (int i = 0; i < 3; i++)
            {
                float z0 = -148.9f + i * 2.4f;
                var o = mb.M;
                mb.M = Matrix4x4.TRS(new Vector3(-2.2f, gy + 7.45f, z0 + 1.2f), Quaternion.identity, Vector3.one);
                mb.For(roofM).BoxRot(new Vector3(0, 0.55f, -0.25f), new Vector3(11.2f, 0.08f, 2.45f), new Vector3(-26f, 0, 0));
                mb.For(glass).Box(new Vector3(0, 0.6f, 0.9f), new Vector3(10.8f, 1.1f, 0.06f));
                for (int k = 0; k < 7; k++) det.For(steelD).Box(new Vector3(-5.2f + k * 1.73f, 0.6f, 0.94f), new Vector3(0.06f, 1.15f, 0.06f));
                mb.M = o;
            }
            // Leuchtröhren-Schriftzug „MIKO“ auf einer Tragschiene über der Attika (flackert nachts)
            neonMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.35f, 0.6f));
            Mats.SetEmission(neonMat, new Color(1f, 0.3f, 0.55f) * 0.6f);
            var rail = steelD;
            mb.For(rail).Box(new Vector3(-0.2f, gy + 7.55f, fz + 0.05f), new Vector3(5.2f, 0.08f, 0.08f));
            for (int k = 0; k < 4; k++) det.For(rail).Box(new Vector3(-2.4f + k * 1.45f, gy + 7.95f, fz - 0.05f), new Vector3(0.05f, 0.85f, 0.05f));
            float lx = -2.2f, ly = gy + 7.65f, lz = fz + 0.12f, H = 0.85f, r = 0.045f;
            void Seg(float x0, float y0, float x1, float y1) { mb.For(neonMat).Tube(new Vector3(lx + x0, ly + y0, lz), new Vector3(lx + x1, ly + y1, lz), r, 6, true); }
            // M
            Seg(0f, 0f, 0f, H); Seg(0f, H, 0.32f, H * 0.45f); Seg(0.32f, H * 0.45f, 0.64f, H); Seg(0.64f, H, 0.64f, 0f);
            // I
            Seg(1.0f, 0f, 1.0f, H);
            // K
            Seg(1.36f, 0f, 1.36f, H); Seg(1.36f, H * 0.45f, 1.84f, H); Seg(1.5f, H * 0.6f, 1.86f, 0f);
            // O (Achteck)
            float ox = 2.5f, oy = H * 0.5f, rx = 0.3f, ry = H * 0.5f;
            for (int k = 0; k < 8; k++)
            {
                float a0 = (k + 0.5f) * Mathf.PI * 0.25f, a1 = (k + 1.5f) * Mathf.PI * 0.25f;
                Seg(ox + Mathf.Cos(a0) * rx, oy + Mathf.Sin(a0) * ry, ox + Mathf.Cos(a1) * rx, oy + Mathf.Sin(a1) * ry);
            }
            // Feuerleiter an der linken Seitenwand (beginnt über 2,4 m – Klappleiter)
            for (int k = 0; k < 2; k++)
            {
                float z = -147.6f + k * 3.8f;
                det.For(steelD).Box(new Vector3(-8.35f, gy + 4.8f, z), new Vector3(0.06f, 4.8f, 0.06f));
                det.For(steelD).Box(new Vector3(-8.35f, gy + 4.8f, z + 0.5f), new Vector3(0.06f, 4.8f, 0.06f));
                for (float y = 2.6f; y < 7.2f; y += 0.35f) det.For(steelD).Box(new Vector3(-8.35f, gy + y, z + 0.25f), new Vector3(0.04f, 0.04f, 0.5f));
            }
            mb.For(steelD).Box(new Vector3(-8.6f, gy + 4.9f, -145.5f), new Vector3(0.7f, 0.06f, 4.4f));
            for (float z = -147.6f; z < -143.2f; z += 0.55f) det.For(steelD).Box(new Vector3(-8.9f, gy + 5.4f, z), new Vector3(0.04f, 1f, 0.04f));
            det.For(steelD).Box(new Vector3(-8.9f, gy + 5.9f, -145.5f), new Vector3(0.05f, 0.05f, 4.4f));
            // Rostfahnen unter den Fenstern, alte Werbetafel-Halterung
            foreach (var x in new[] { -4.0f, 2.4f }) det.For(Paint(new Color(0.42f, 0.25f, 0.16f))).Box(new Vector3(x, gy + 4.0f, fz + 0.021f), new Vector3(0.5f, 0.9f, 0.01f));
        }

        // ================================================================== PYRA: Gießerei-Bunker im Fels
        void PyraBunker(MultiBuilder mb, MultiBuilder det, float gy)
        {
            const float fz = -141.5f;
            var conc = Conc(new Color(0.55f, 0.46f, 0.42f));
            var concD = Conc(new Color(0.42f, 0.35f, 0.32f));
            var rust = Paint(new Color(0.48f, 0.26f, 0.16f), 0.35f);
            var steelD = Steel(new Color(0.22f, 0.2f, 0.2f));
            var rock = Mats.Surface(SurfKind.Stone, Q(new Color(0.66f, 0.3f, 0.2f)));
            var rockD = Mats.Surface(SurfKind.Stone, Q(new Color(0.5f, 0.22f, 0.15f)));
            var hazard = Paint(new Color(0.95f, 0.7f, 0.12f));
            HallShell(mb, gy, conc, 7.2f);
            // Schalungsfugen und Sockel
            WallBands(det, gy, concD, 1.2f, 7.0f, 1.2f, 0.03f, 0.05f);
            mb.For(concD).BevelBox(new Vector3(-5.2f, gy + 0.45f, fz + 0.08f), new Vector3(5.6f, 0.9f, 0.16f), 0.04f);
            // Betondecke, schräge Stirn über dem Tor (Bunkerbraue), Torzarge mit Führungsschienen
            mb.For(conc).BevelBox(new Vector3(-2.2f, gy + 7.5f, -145.4f), new Vector3(12.4f, 0.7f, 8.8f), 0.12f);
            mb.For(conc).BevelBoxRot(new Vector3(-0.1f, gy + 5.2f, fz + 0.75f), new Vector3(7.4f, 0.5f, 1.8f), new Vector3(16f, 0, 0), 0.1f);
            foreach (var sx in new[] { -2.85f, 2.85f })
            {
                mb.For(steelD).BevelBox(new Vector3(sx, gy + 4.6f, fz + 0.18f), new Vector3(0.36f, 9.2f, 0.3f), 0.04f);
                for (int k = 0; k < 7; k++) mb.For(hazard).BoxRot(new Vector3(sx, gy + 0.3f + k * 0.6f, fz + 0.34f), new Vector3(0.37f, 0.18f, 0.02f), new Vector3(0, 0, 35f));
            }
            mb.For(steelD).BevelBox(new Vector3(0f, gy + 9.0f, fz + 0.2f), new Vector3(6.2f, 0.6f, 0.5f), 0.05f); // Torkasten oben
            // Fels: Flanke links (Kollision WorldGen.StationExtras), Fels hinter und auf dem Bunker
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(-10.0f, gy - 0.6f, -146.8f), Quaternion.Euler(0, 6, 0), new Vector3(1.9f, 1f, 3.6f));
            mb.For(rock).Blob(Vector3.zero, 1f, 6.4f, 14, 5, 71, 0.18f);
            mb.M = Matrix4x4.TRS(new Vector3(-9.6f, gy + 3.2f, -147.4f), Quaternion.Euler(0, -12, 0), new Vector3(1.5f, 1f, 2.4f));
            mb.For(rockD).Blob(Vector3.zero, 1f, 4.6f, 12, 4, 73, 0.22f);
            mb.M = Matrix4x4.TRS(new Vector3(-3.0f, gy + 7.6f, -147.6f), Quaternion.Euler(0, 4, 0), new Vector3(6.4f, 1f, 2.6f));
            mb.For(rock).Blob(Vector3.zero, 1f, 2.6f, 16, 4, 75, 0.2f);
            mb.M = Matrix4x4.TRS(new Vector3(-2.0f, gy - 1f, -152.2f), Quaternion.identity, new Vector3(11.5f, 1f, 2.6f));
            mb.For(rockD).Blob(Vector3.zero, 1f, 10.5f, 18, 5, 77, 0.2f);
            mb.M = o;
            for (int k = 0; k < 7; k++)
            {
                float x = -11.5f + (k * 3.1f) % 9f, z = -146f + (k % 3) * 1.4f;
                det.For(k % 2 == 0 ? rock : rockD).Crumple(new Vector3(-3f + x * 0.9f, gy + 8.3f + (k % 2) * 0.3f, z - 2f), 0.5f + (k % 3) * 0.2f, 0.7f, 90 + k, 0.3f, 7, 4);
            }
            // Erzbunker statt Silos: Rostbleche mit Bandagen, Trichterhauben, Laufsteg
            for (int s = 0; s < 2; s++)
            {
                var p = new Vector3(6.1f, gy, s == 0 ? -143.5f : -147.4f);
                mb.For(concD).Cylinder(p, 1.95f, 0.6f, 16, true);
                mb.For(rust).Cylinder(p + Vector3.up * 0.6f, 1.75f, 8.6f, 18, false);
                mb.For(steelD).Cylinder(p + Vector3.up * 9.2f, 1.8f, 1.4f, 18, true, 0.5f);
                for (float y = 1.4f; y < 9f; y += 1.6f) det.For(steelD).Torus(p + Vector3.up * y, 1.78f, 0.06f, 18, 4);
                for (int k = 0; k < 6; k++) det.For(Paint(new Color(0.35f, 0.18f, 0.12f))).Box(p + Quaternion.Euler(0, k * 60f + 15f, 0) * new Vector3(0, 5f, 1.76f), new Vector3(0.35f, 7.5f, 0.02f));
            }
            mb.For(steelD).Box(new Vector3(6.1f, gy + 9.9f, -145.45f), new Vector3(1.2f, 0.08f, 2.4f));
            // Schornsteine mit Glut (Rauch: AnimateBaseLights)
            ventMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.45f, 0.12f));
            Mats.SetEmission(ventMat, new Color(1.6f, 0.5f, 0.12f));
            for (int k = 0; k < 3; k++)
            {
                var b = new Vector3(-6.5f + k * 2.6f, gy + 7.8f, -148.2f);
                float h = 6.5f + k * 1.6f;
                mb.For(steelD).Cylinder(b, 0.42f, h, 14, false, 0.36f);
                mb.For(rust).Cylinder(b + Vector3.up * (h - 0.4f), 0.44f, 0.5f, 14, false);
                for (float y = 1.2f; y < h - 0.6f; y += 1.7f) det.For(rust).Torus(b + Vector3.up * y, 0.41f, 0.05f, 14, 3);
                mb.For(ventMat).Disc(b + Vector3.up * (h - 0.05f), 0.34f, 12, true);
                chimneyTops.Add(b + Vector3.up * (h + 0.2f));
            }
            // Rohrbrücke an der Front und zu den Bunkern, Ventilräder
            var pipe = Steel(new Color(0.5f, 0.45f, 0.42f));
            mb.For(pipe).CylinderX(new Vector3(-1.5f, gy + 6.4f, fz + 0.45f), 0.28f, 13.4f, 12);
            mb.For(pipe).CylinderX(new Vector3(-1.5f, gy + 5.85f, fz + 0.4f), 0.16f, 13.4f, 10);
            for (int k = 0; k < 4; k++)
            {
                float x = -7.4f + k * 3.6f;
                det.For(steelD).Box(new Vector3(x, gy + 6.2f, fz + 0.25f), new Vector3(0.12f, 0.9f, 0.4f));
                det.For(hazard).TorusRot(new Vector3(x + 1.2f, gy + 6.4f, fz + 0.8f), new Vector3(90, 0, 0), 0.22f, 0.03f, 12, 4);
            }
            mb.For(pipe).Tube(new Vector3(-7.4f, gy + 6.4f, fz + 0.45f), new Vector3(-7.4f, gy + 0.2f, fz + 0.2f), 0.2f, 10);
            // Abluftgitter mit Glut (pulsieren)
            for (int k = 0; k < 3; k++)
            {
                var vp = new Vector3(-6.6f + k * 1.4f, gy + 1.1f, fz + 0.02f);
                mb.For(steelD).BevelBox(vp, new Vector3(1.1f, 0.6f, 0.08f), 0.02f);
                for (int g = 0; g < 4; g++) mb.For(ventMat).Box(vp + new Vector3(0, -0.2f + g * 0.13f, 0.035f), new Vector3(0.9f, 0.04f, 0.02f));
            }
            BaseLamp(mb, new Vector3(-4.4f, gy + 3.6f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(4.4f, gy + 3.6f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(0f, gy + 4.95f, fz + 1.45f), Vector3.down);
            IconSign(mb, new Vector3(-5.6f, gy + 4.9f, fz + 0.1f), 0, 1.3f, SurfaceLook.Icon.Recycle, new Color(1f, 0.5f, 0.2f), false);
            IconSign(mb, new Vector3(6.1f, gy + 3.2f, -141.66f), 0, 0.85f, SurfaceLook.Icon.Crate, new Color(1f, 0.55f, 0.2f), false);
            // Warnleuchte
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(6.1f, gy + 10.9f, -145.45f), 0.2f, 6, 4);
        }

        // ================================================================== PELAGIA: Pfahlbau-Station mit Bootshaus
        void PelagiaPier(MultiBuilder mb, MultiBuilder det, float gy)
        {
            const float fz = -141.5f;
            var board = Wood(new Color(0.56f, 0.7f, 0.74f), 0.35f);
            var boardD = Wood(new Color(0.42f, 0.53f, 0.57f), 0.3f);
            var trim = Wood(new Color(0.93f, 0.92f, 0.88f), 0.4f);
            var plank = Wood(new Color(0.55f, 0.42f, 0.29f));
            var plankD = Wood(new Color(0.42f, 0.31f, 0.22f));
            var tin = Paint(new Color(0.66f, 0.3f, 0.24f), 0.45f);
            var dark = Paint(new Color(0.12f, 0.13f, 0.14f));
            var rope = Mats.Surface(SurfKind.Rubber, Q(new Color(0.78f, 0.7f, 0.5f)), 0.2f);
            var red = Paint(new Color(0.88f, 0.2f, 0.16f), 0.5f);
            var white = Paint(new Color(0.94f, 0.94f, 0.92f), 0.5f);
            HallShell(mb, gy, board, 7.0f);
            // Stelzen-Sockel: dunkler Unterbau mit Pfählen und Kreuzverband (innerhalb der Wandstärke)
            mb.For(dark).Box(new Vector3(-5.2f, gy + 0.35f, fz + 0.02f), new Vector3(5.6f, 0.7f, 0.06f));
            mb.For(dark).Box(new Vector3(-8.02f, gy + 0.35f, -145.5f), new Vector3(0.06f, 0.7f, 8.1f));
            for (float x = -7.8f; x < -2.5f; x += 1.3f)
            {
                det.For(plankD).Box(new Vector3(x, gy + 0.35f, fz + 0.06f), new Vector3(0.2f, 0.7f, 0.06f));
                det.For(plankD).BoxRot(new Vector3(x + 0.65f, gy + 0.35f, fz + 0.06f), new Vector3(0.08f, 0.95f, 0.03f), new Vector3(0, 0, 50));
            }
            // Bretterstöße der Verschalung, Eckbretter, Fensterrahmen
            WallBands(det, gy, boardD, 0.95f, 6.9f, 0.42f, 0.03f, 0.04f);
            foreach (var x in new[] { -7.95f, -2.75f, 2.75f }) mb.For(trim).Box(new Vector3(x, gy + 3.5f, fz + 0.05f), new Vector3(0.22f, 7f, 0.08f));
            foreach (var x in new[] { -4.6f, 2.0f })
            {
                mb.For(trim).Box(new Vector3(x, gy + 5.3f, fz + 0.03f), new Vector3(1.5f, 1.2f, 0.05f));
                mb.For(WindowMat(0)).Box(new Vector3(x, gy + 5.3f, fz + 0.06f), new Vector3(1.25f, 0.95f, 0.04f));
                det.For(trim).Box(new Vector3(x, gy + 5.3f, fz + 0.09f), new Vector3(0.06f, 0.95f, 0.04f));
            }
            // Satteldach aus Wellblech (First entlang x) mit Giebelbrettern
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(-2.2f, gy + 7.0f, -145.5f), Quaternion.identity, Vector3.one);
            mb.For(tin).Prism(Vector3.zero, new Vector3(12.4f, 2.6f, 9.4f));
            mb.For(board).Prism(new Vector3(0, -0.01f, 0), new Vector3(11.6f, 2.55f, 8f));
            for (float x = -5.8f; x < 6f; x += 0.6f)
                det.For(Paint(new Color(0.56f, 0.24f, 0.19f))).BoxRot(new Vector3(x, 1.32f, 2.35f), new Vector3(0.05f, 0.05f, 4.9f), new Vector3(Mathf.Atan2(2.6f, 4.7f) * Mathf.Rad2Deg, 0, 0));
            mb.M = o;
            // Bohlensteg vor dem Tor (flach, befahrbar) mit Randbalken
            for (int k = 0; k < 18; k++)
                mb.For(k % 4 == 0 ? plankD : plank).Box(new Vector3(-0.3f, gy + 0.018f, -141.15f + k * 0.3f), new Vector3(19.4f, 0.036f, 0.27f));
            mb.For(plankD).Box(new Vector3(-0.3f, gy + 0.03f, -135.85f), new Vector3(19.4f, 0.06f, 0.16f));
            // Wassertanks statt Silos (weiß-rot, Fassreifen), oben das Leuchtfeuer
            for (int s = 0; s < 2; s++)
            {
                var p = new Vector3(6.1f, gy, s == 0 ? -143.5f : -147.4f);
                mb.For(plankD).Cylinder(p, 1.9f, 0.7f, 18, true);
                mb.For(white).Cylinder(p + Vector3.up * 0.7f, 1.75f, 8.8f, 20, false);
                mb.For(red).Cylinder(p + Vector3.up * 6.2f, 1.77f, 1.0f, 20, false);
                mb.For(tin).Cylinder(p + Vector3.up * 9.5f, 1.85f, 1.0f, 20, true, 0.3f);
                for (float y = 1.6f; y < 9.4f; y += 1.9f) det.For(steelD()).Torus(p + Vector3.up * y, 1.77f, 0.05f, 20, 3);
            }
            var lt = new Vector3(6.1f, gy + 10.5f, -145.45f);
            mb.For(white).Cylinder(lt, 1.1f, 2.2f, 16, true, 0.95f);
            mb.For(red).Cylinder(lt + Vector3.up * 2.2f, 1.2f, 0.25f, 16, true);
            mb.For(Mats.Get(Mats.Fade, new Color(0.75f, 0.9f, 1f, 0.35f), null, 0.95f)).Cylinder(lt + Vector3.up * 2.45f, 0.85f, 1.2f, 12, false);
            beaconMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.95f, 0.75f));
            Mats.SetEmission(beaconMat, new Color(1f, 0.9f, 0.6f) * 0.4f);
            mb.For(beaconMat).Sphere(lt + Vector3.up * 3.05f, 0.38f, 10, 6);
            mb.For(red).Cylinder(lt + Vector3.up * 3.65f, 1.0f, 0.6f, 16, true, 0.15f);
            for (int k = 0; k < 6; k++) det.For(steelD()).Box(lt + Vector3.up * 3.05f + Quaternion.Euler(0, k * 60f, 0) * new Vector3(0, 0, 0.85f), new Vector3(0.05f, 1.2f, 0.05f));
            // Bootshaus über dem Hafenbecken (Kollision WorldGen.StationExtras: x −19,5…−10,5, z −150…−143,5)
            float bx0 = -19.5f, bx1 = -10.5f, bz0 = -150f, bz1 = -143.5f, bcx = (bx0 + bx1) * 0.5f, bcz = (bz0 + bz1) * 0.5f;
            var quay = Conc(new Color(0.5f, 0.52f, 0.5f));
            mb.For(quay).Box(new Vector3(bcx, gy - 0.4f, bz1 - 0.25f), new Vector3(bx1 - bx0, 1.2f, 0.5f));
            mb.For(quay).Box(new Vector3(bx0 + 0.25f, gy - 0.4f, bcz), new Vector3(0.5f, 1.2f, bz1 - bz0));
            mb.For(quay).Box(new Vector3(bx1 - 0.25f, gy - 0.4f, bcz), new Vector3(0.5f, 1.2f, bz1 - bz0));
            mb.For(plank).Box(new Vector3(bcx, gy + 0.24f, bz1 - 0.25f), new Vector3(bx1 - bx0 + 0.1f, 0.08f, 0.6f));
            mb.For(Mats.Get(Mats.Water, new Color(0.18f, 0.45f, 0.5f, 0.75f), null, 0.95f)).Box(new Vector3(bcx, gy - 0.55f, bcz), new Vector3(bx1 - bx0 - 1f, 0.02f, bz1 - bz0 - 0.5f));
            mb.For(Conc(new Color(0.2f, 0.28f, 0.3f))).Box(new Vector3(bcx, gy - 1.3f, bcz), new Vector3(bx1 - bx0 - 1f, 0.05f, bz1 - bz0 - 0.5f));
            // Geländer am Becken (Kollision bis 1,1 m)
            for (float x = bx0 + 0.3f; x <= bx1 - 0.2f; x += 1.1f) det.For(white).Box(new Vector3(x, gy + 0.75f, bz1 - 0.15f), new Vector3(0.06f, 0.95f, 0.06f));
            mb.For(white).Box(new Vector3(bcx, gy + 1.2f, bz1 - 0.15f), new Vector3(bx1 - bx0, 0.07f, 0.07f));
            mb.For(white).Box(new Vector3(bcx, gy + 0.75f, bz1 - 0.15f), new Vector3(bx1 - bx0, 0.05f, 0.05f));
            // Boot im Becken
            var hull = Paint(new Color(0.95f, 0.55f, 0.18f), 0.5f);
            o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(bcx - 0.4f, gy - 0.75f, bcz - 0.3f), Quaternion.Euler(0, 90, 0), new Vector3(1f, 1f, 2.1f));
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.7f, 0.08f), new Vector2(1.0f, 0.4f), new Vector2(1.08f, 0.75f) }, 14, true);
            mb.M = o;
            mb.For(white).BevelBox(new Vector3(bcx - 0.4f, gy + 0.05f, bcz - 0.8f), new Vector3(1.3f, 0.8f, 1.1f), 0.06f);
            // Dach auf Pfosten (über 4 m), Netz unter dem Dach
            foreach (var px in new[] { bx0 + 0.3f, bx1 - 0.3f })
                foreach (var pz in new[] { bz0 + 0.3f, bz1 - 0.3f })
                    mb.For(plankD).Box(new Vector3(px, gy + 2.2f, pz), new Vector3(0.24f, 4.4f, 0.24f));
            o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(bcx, gy + 4.4f, bcz), Quaternion.Euler(0, 90, 0), Vector3.one);
            mb.For(tin).Prism(Vector3.zero, new Vector3(7.2f, 1.6f, 9.8f));
            mb.M = o;
            det.For(rope).BoxRot(new Vector3(bcx + 2f, gy + 3.2f, bz1 - 0.4f), new Vector3(2.4f, 1.6f, 0.03f), new Vector3(0, 0, 8));
            // Davit-Kran an der Beckenecke
            var davit = Paint(new Color(0.95f, 0.75f, 0.15f));
            var dp = new Vector3(bx1 - 0.3f, gy + 0.25f, bz1 - 0.3f);
            mb.For(davit).Cylinder(dp, 0.13f, 4.6f, 10);
            mb.For(davit).Beam(dp + Vector3.up * 4.5f, dp + new Vector3(-3.0f, 4.1f, -1.2f), 0.14f);
            mb.For(davit).Beam(dp + Vector3.up * 3.0f, dp + new Vector3(-1.6f, 4.25f, -0.64f), 0.07f);
            det.For(rope).Tube(dp + new Vector3(-3.0f, 4.0f, -1.2f), dp + new Vector3(-3.0f, 1.2f, -1.2f), 0.02f, 4);
            det.For(steelD()).TorusRot(dp + new Vector3(-3.0f, 1.1f, -1.2f), new Vector3(90, 0, 0), 0.1f, 0.025f, 10, 4);
            // Bojen, Rettungsring, Fischkisten, Taue an der Front
            for (int k = 0; k < 3; k++)
            {
                var bp = new Vector3(-6.9f + k * 0.55f, gy + 3.3f - (k % 2) * 0.25f, fz + 0.32f);
                mb.For(k % 2 == 0 ? red : white).Sphere(bp, 0.24f, 10, 7);
                det.For(rope).Tube(bp + Vector3.up * 0.22f, new Vector3(-6.4f, gy + 4.1f, fz + 0.06f), 0.015f, 4);
            }
            mb.For(red).TorusRot(new Vector3(-3.6f, gy + 2.8f, fz + 0.08f), new Vector3(90, 0, 0), 0.32f, 0.09f, 16, 6);
            det.For(white).TorusRot(new Vector3(-3.6f, gy + 2.8f, fz + 0.1f), new Vector3(90, 0, 0), 0.32f, 0.093f, 4, 6);
            for (int k = 0; k < 3; k++) det.For(Paint(k % 2 == 0 ? new Color(0.25f, 0.5f, 0.75f) : new Color(0.95f, 0.55f, 0.2f))).BevelBox(new Vector3(bx1 - 0.3f, gy + 1.3f + k * 0.32f, bz0 + 1.6f + (k % 2) * 0.1f), new Vector3(0.5f, 0.3f, 0.7f), 0.03f);
            BaseLamp(mb, new Vector3(-5.4f, gy + 3.9f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(2.4f, gy + 3.9f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(bcx, gy + 3.9f, bz1 - 0.6f), Vector3.down);
            IconSign(mb, new Vector3(-0.2f, gy + 5.9f, fz + 0.1f), 0, 1.4f, SurfaceLook.Icon.Recycle, new Color(0.18f, 0.72f, 0.68f), false);
            IconSign(mb, new Vector3(6.1f, gy + 3.2f, -141.66f), 0, 0.85f, SurfaceLook.Icon.Crate, new Color(0.2f, 0.75f, 0.8f), false);
        }

        Material steelD() { return Steel(new Color(0.24f, 0.25f, 0.27f)); }

        // ================================================================== NIVALIS: Forschungskuppel
        void NivalisDome(MultiBuilder mb, MultiBuilder det, float gy)
        {
            const float fz = -141.5f;
            var panel = Clad(new Color(0.86f, 0.9f, 0.95f), 0.45f);
            var seam = Paint(new Color(0.62f, 0.68f, 0.76f));
            var orange = Paint(new Color(0.95f, 0.45f, 0.15f), 0.5f);
            var navy = Paint(new Color(0.2f, 0.26f, 0.38f), 0.45f);
            var snow = Mats.Surface(SurfKind.Generic, Q(new Color(0.95f, 0.97f, 1f)), 0.35f);
            var steelD = Steel(new Color(0.3f, 0.33f, 0.38f));
            HallShell(mb, gy, panel, 6.6f);
            // Paneelfugen (senkrecht) und Sockelband, gerundete Eckwülste
            for (float x = -7.2f; x < 2.6f; x += 1.2f)
            {
                if (Mathf.Abs(x) < 2.8f) continue;
                det.For(seam).Box(new Vector3(x, gy + 3.3f, fz + 0.015f), new Vector3(0.04f, 6.6f, 0.03f));
            }
            for (float z = -149f; z < -142f; z += 1.2f) det.For(seam).Box(new Vector3(-8.015f, gy + 3.3f, z), new Vector3(0.03f, 6.6f, 0.04f));
            mb.For(navy).Box(new Vector3(-5.2f, gy + 0.4f, fz + 0.03f), new Vector3(5.6f, 0.8f, 0.06f));
            mb.For(orange).Box(new Vector3(-2.2f, gy + 2.8f, fz + 0.03f), new Vector3(11.6f, 0.3f, 0.06f));
            mb.For(orange).Box(new Vector3(-8.03f, gy + 2.8f, -145.5f), new Vector3(0.06f, 0.3f, 8f));
            foreach (var c in new[] { new Vector3(-7.62f, 0, -141.92f), new Vector3(-7.62f, 0, -149.08f) })
                mb.For(panel).Cylinder(new Vector3(c.x, gy, c.z), 0.42f, 6.6f, 12);
            // Kuppeldach (Rotationsschale, elliptisch gestaucht) mit Ringband und Schneehaube
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(-2.2f, gy + 6.5f, -145.5f), Quaternion.identity, new Vector3(6.3f, 3.2f, 4.4f));
            mb.For(panel).Lathe(Vector3.zero, new[] { new Vector2(1f, 0f), new Vector2(0.97f, 0.25f), new Vector2(0.86f, 0.52f), new Vector2(0.66f, 0.76f), new Vector2(0.38f, 0.93f), new Vector2(0f, 1f) }, 28);
            mb.M = Matrix4x4.TRS(new Vector3(-2.2f, gy + 6.52f, -145.5f), Quaternion.identity, new Vector3(6.32f, 3.22f, 4.42f));
            mb.For(snow).Lathe(Vector3.zero, new[] { new Vector2(0.72f, 0.7f), new Vector2(0.5f, 0.88f), new Vector2(0.25f, 0.97f), new Vector2(0f, 1.01f) }, 28);
            mb.M = o;
            mb.For(orange).Box(new Vector3(-2.2f, gy + 6.55f, fz + 0.05f), new Vector3(11.8f, 0.2f, 0.1f));
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                var bp = new Vector3(-2.2f + Mathf.Cos(a) * 6.35f, gy + 6.6f, -145.5f + Mathf.Sin(a) * 4.45f);
                det.For(steelD).Box(bp, new Vector3(0.18f, 0.25f, 0.18f));
            }
            // Rundes Schleusentor: zwei Ringe um die Öffnung, Kuppelvorbau (über 4,3 m)
            mb.For(navy).TorusRot(new Vector3(0, gy + 2.0f, fz + 0.18f), new Vector3(90, 0, 0), 3.4f, 0.28f, 40, 8);
            mb.For(orange).TorusRot(new Vector3(0, gy + 2.0f, fz + 0.4f), new Vector3(90, 0, 0), 3.62f, 0.12f, 40, 6);
            for (int k = 0; k < 12; k++)
            {
                float a = (k / 12f) * Mathf.PI * 2f;
                if (Mathf.Sin(a) < -0.45f) continue;
                det.For(steelD).Box(new Vector3(Mathf.Cos(a) * 3.4f, gy + 2.0f + Mathf.Sin(a) * 3.4f, fz + 0.5f), new Vector3(0.14f, 0.14f, 0.1f));
            }
            mb.For(panel).BevelBoxRot(new Vector3(0, gy + 5.55f, fz + 0.65f), new Vector3(6.6f, 0.18f, 1.4f), new Vector3(12f, 0, 0), 0.05f);
            mb.For(snow).BoxRot(new Vector3(0, gy + 5.68f, fz + 0.65f), new Vector3(6.4f, 0.12f, 1.3f), new Vector3(12f, 0, 0));
            // Kryotanks statt Silos: weiß mit blauen Bändern, Reifkragen, Schneekappen
            for (int s = 0; s < 2; s++)
            {
                var p = new Vector3(6.1f, gy, s == 0 ? -143.5f : -147.4f);
                mb.For(navy).Cylinder(p, 1.9f, 0.6f, 18, true);
                mb.For(panel).Cylinder(p + Vector3.up * 0.6f, 1.75f, 9f, 22, false);
                mb.For(navy).Cylinder(p + Vector3.up * 3.2f, 1.77f, 0.5f, 22, false);
                mb.For(navy).Cylinder(p + Vector3.up * 7.4f, 1.77f, 0.5f, 22, false);
                mb.For(panel).Lathe(p + Vector3.up * 9.6f, new[] { new Vector2(1.75f, 0f), new Vector2(1.5f, 0.6f), new Vector2(0.9f, 1.05f), new Vector2(0f, 1.2f) }, 22);
                mb.For(snow).Lathe(p + Vector3.up * 9.62f, new[] { new Vector2(1.2f, 0.9f), new Vector2(0.6f, 1.15f), new Vector2(0f, 1.24f) }, 22);
            }
            // Antennenschüsseln, Radom, Mast mit Leuchtfeuer
            var white = Paint(new Color(0.92f, 0.94f, 0.97f), 0.5f);
            for (int k = 0; k < 2; k++)
            {
                var mp = new Vector3(-6.0f + k * 5.4f, gy + 8.8f, -147.2f + k * 0.8f);
                mb.For(steelD).Cylinder(mp + Vector3.down * 0.4f, 0.12f, 1.6f, 8);
                o = mb.M;
                mb.M = Matrix4x4.TRS(mp + Vector3.up * 1.3f, Quaternion.Euler(-48f, k == 0 ? 160f : 210f, 0), Vector3.one);
                mb.For(white).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.7f, 0.18f), new Vector2(1.15f, 0.48f), new Vector2(1.2f, 0.52f) }, 18, true);
                mb.For(steelD).Cylinder(Vector3.zero, 0.035f, 0.95f, 5);
                mb.For(steelD).Sphere(new Vector3(0, 0.95f, 0), 0.07f, 6, 4);
                mb.M = o;
            }
            mb.For(white).Sphere(new Vector3(6.1f, gy + 12.2f, -145.45f), 1.1f, 16, 10);
            mb.For(steelD).Cylinder(new Vector3(6.1f, gy + 10.5f, -145.45f), 0.4f, 0.9f, 10);
            mb.For(steelD).Cylinder(new Vector3(-0.2f, gy + 9.4f, -148.6f), 0.08f, 5.2f, 6);
            beaconMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.3f, 0.15f));
            Mats.SetEmission(beaconMat, new Color(2f, 0.4f, 0.15f));
            mb.For(beaconMat).Sphere(new Vector3(-0.2f, gy + 14.7f, -148.6f), 0.2f, 6, 4);
            // Heizstrahler links und rechts vom Tor (glühen, nachts stärker)
            heatMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.4f, 0.18f));
            Mats.SetEmission(heatMat, new Color(1.4f, 0.35f, 0.1f));
            foreach (var sx in new[] { -4.4f, 4.0f })
            {
                var hp = new Vector3(sx, gy + 3.7f, fz + 0.5f);
                mb.For(steelD).Beam(new Vector3(sx, gy + 4.1f, fz + 0.02f), hp + Vector3.up * 0.15f, 0.06f);
                mb.For(steelD).BevelBoxRot(hp, new Vector3(0.7f, 0.12f, 0.36f), new Vector3(30, 0, 0), 0.03f);
                for (int g = 0; g < 3; g++) mb.For(heatMat).BoxRot(hp + new Vector3(-0.22f + g * 0.22f, -0.05f, 0.03f), new Vector3(0.05f, 0.03f, 0.3f), new Vector3(30, 0, 0));
            }
            BaseLamp(mb, new Vector3(-6.2f, gy + 4.2f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(2.0f, gy + 6.0f, fz + 0.02f), Vector3.forward);
            IconSign(mb, new Vector3(-5.4f, gy + 4.9f, fz + 0.1f), 0, 1.3f, SurfaceLook.Icon.Recycle, new Color(0.3f, 0.7f, 1f), false);
            IconSign(mb, new Vector3(6.1f, gy + 3.2f, -141.66f), 0, 0.85f, SurfaceLook.Icon.Crate, new Color(0.3f, 0.7f, 1f), false);
            // Schneewehen an der Rückwand (hinter der Halle) und Eiszapfen an der Kuppelkante
            o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(-1.5f, gy - 0.2f, -150.4f), Quaternion.identity, new Vector3(9f, 1f, 1.4f));
            mb.For(snow).Blob(Vector3.zero, 1f, 1.6f, 16, 4, 51, 0.2f);
            mb.M = o;
            for (int k = 0; k < 14; k++)
            {
                float x = -7.6f + k * 0.75f;
                if (Mathf.Abs(x) < 3.3f) continue;
                { float len = 0.25f + (k % 3) * 0.15f; det.For(Mats.Get(Mats.Opaque, new Color(0.8f, 0.9f, 1f), null, 0.9f)).Cylinder(new Vector3(x, gy + 6.25f - len, fz + 0.06f), 0.005f, len, 5, false, 0.05f); }
            }
        }

        // ================================================================== Innenraum je Planet
        /// <summary>Planetentypische Ausstattung im Hangar (über die gemeinsame Grundausstattung gelegt).</summary>
        void HangarStyleDetails(MultiBuilder mb, MultiBuilder det, float gy)
        {
            const float x0 = -7.6f, x1 = 2.6f, z0 = -149.1f, z1 = -141.9f, cx = -2.5f, cz = -145.5f;
            switch (stationStyle)
            {
                case 0:
                    // Backstein innen sichtbar (Bänder), alte Fabrikleuchten
                    for (float y = 1.6f; y < 6.4f; y += 0.9f) det.For(Brick(new Color(0.5f, 0.27f, 0.21f))).Box(new Vector3(cx, gy + y, z0 + 0.02f), new Vector3(x1 - x0, 0.08f, 0.03f));
                    break;
                case 1:
                    {
                        // Betongewölbe: Bogenrippen, Rohre an der Decke, rote Notleuchte, Brandspuren
                        var rib = Conc(new Color(0.4f, 0.34f, 0.31f));
                        for (int k = 0; k < 4; k++)
                        {
                            float z = -148.4f + k * 2f;
                            for (int s = 0; s < 6; s++)
                            {
                                float a0 = s / 6f * Mathf.PI, a1 = (s + 1) / 6f * Mathf.PI;
                                var p0 = new Vector3(cx + Mathf.Cos(a0) * 5f, gy + 4.6f + Mathf.Sin(a0) * 1.9f, z);
                                var p1 = new Vector3(cx + Mathf.Cos(a1) * 5f, gy + 4.6f + Mathf.Sin(a1) * 1.9f, z);
                                mb.For(rib).Beam(p0, p1, 0.28f, 0.2f);
                            }
                        }
                        var pipe = Steel(new Color(0.55f, 0.32f, 0.2f));
                        mb.For(pipe).CylinderZ(new Vector3(x0 + 0.6f, gy + 5.6f, cz), 0.16f, z1 - z0, 10);
                        mb.For(pipe).CylinderZ(new Vector3(x0 + 1.0f, gy + 5.9f, cz), 0.1f, z1 - z0, 8);
                        mb.For(Glow(new Color(1f, 0.15f, 0.08f), 2.4f)).Sphere(new Vector3(x1 - 0.2f, gy + 4.6f, z0 + 0.4f), 0.12f, 6, 4);
                        det.For(Paint(new Color(0.18f, 0.14f, 0.13f))).Box(new Vector3(-4.5f, gy + 0.033f, -146.5f), new Vector3(1.6f, 0.005f, 1.1f));
                        break;
                    }
                case 2:
                    {
                        // Holzbalken, Netz an der Wand, Ruder auf dem Regal, Laternen
                        var beam = Wood(new Color(0.4f, 0.3f, 0.21f));
                        for (int k = 0; k < 4; k++) mb.For(beam).Box(new Vector3(cx, gy + 6.0f, -148.3f + k * 2.1f), new Vector3(x1 - x0, 0.3f, 0.24f));
                        mb.For(beam).Box(new Vector3(cx, gy + 6.2f, cz), new Vector3(0.24f, 0.24f, z1 - z0));
                        var rope = Mats.Surface(SurfKind.Rubber, Q(new Color(0.65f, 0.6f, 0.45f)), 0.2f);
                        det.For(rope).Box(new Vector3(-4.6f, gy + 3.6f, z0 + 0.04f), new Vector3(2.4f, 1.6f, 0.02f));
                        for (int k = 0; k < 6; k++) det.For(rope).Box(new Vector3(-5.8f + k * 0.48f, gy + 3.6f, z0 + 0.06f), new Vector3(0.015f, 1.6f, 0.015f));
                        det.For(Paint(new Color(0.88f, 0.2f, 0.16f))).TorusRot(new Vector3(x0 + 0.06f, gy + 3.9f, -144.6f), new Vector3(0, 0, 90), 0.32f, 0.08f, 14, 5);
                        det.For(Wood(new Color(0.6f, 0.46f, 0.3f))).BoxRot(new Vector3(-7.25f, gy + 3.0f, -145.65f), new Vector3(0.08f, 0.06f, 2.6f), new Vector3(0, 0, 8));
                        break;
                    }
                case 3:
                    {
                        // gerippte Wandpaneele, Lichtbänder, Heizkörper an der Seitenwand
                        var panel = Clad(new Color(0.82f, 0.87f, 0.93f));
                        for (int k = 0; k < 5; k++)
                        {
                            float z = -148.6f + k * 1.7f;
                            mb.For(panel).Box(new Vector3(x0 + 0.08f, gy + 3.4f, z), new Vector3(0.1f, 6.2f, 0.16f));
                            mb.For(panel).Box(new Vector3(cx, gy + 6.35f, z), new Vector3(x1 - x0, 0.14f, 0.16f));
                        }
                        foreach (var sx in new[] { -5f, -0.2f }) mb.For(cabinMat).Box(new Vector3(sx, gy + 6.25f, cz), new Vector3(0.12f, 0.03f, z1 - z0 - 0.6f));
                        var hp = new Vector3(x1 - 0.12f, gy + 1.4f, -143.4f);
                        mb.For(Steel(new Color(0.3f, 0.33f, 0.38f))).BevelBox(hp, new Vector3(0.12f, 0.6f, 1.1f), 0.02f);
                        for (int g = 0; g < 5; g++) mb.For(heatMat ?? Glow(new Color(1f, 0.4f, 0.18f), 1.6f)).Box(hp + new Vector3(-0.065f, 0, -0.4f + g * 0.2f), new Vector3(0.01f, 0.45f, 0.05f));
                        break;
                    }
            }
        }

        // ================================================================== Garage je Planet
        void StationGarage(MultiBuilder mb, MultiBuilder det, float gy)
        {
            if (stationStyle == 0) { GarageBuilding(mb, gy); return; }
            const float fz = -142.5f;
            Material wall, roof;
            switch (stationStyle)
            {
                case 1: wall = Paint(new Color(0.5f, 0.3f, 0.2f), 0.35f); roof = Steel(new Color(0.35f, 0.25f, 0.2f)); break;
                case 2: wall = Wood(new Color(0.36f, 0.52f, 0.62f)); roof = Paint(new Color(0.66f, 0.3f, 0.24f), 0.45f); break;
                default: wall = Clad(new Color(0.88f, 0.91f, 0.95f)); roof = Paint(new Color(0.95f, 0.45f, 0.15f), 0.5f); break;
            }
            var dark = darkMat;
            if (stationStyle == 3)
            {
                // Rundbogenhalle (Halbzylinder entlang z)
                var o = mb.M;
                mb.M = Matrix4x4.TRS(new Vector3(-26f, gy, -146f), Quaternion.identity, Vector3.one);
                for (int k = 0; k < 10; k++)
                {
                    float a0 = k / 10f * Mathf.PI, a1 = (k + 1) / 10f * Mathf.PI;
                    var p0 = new Vector3(Mathf.Cos(a0) * 4.9f, Mathf.Sin(a0) * 4.9f, 0); var p1 = new Vector3(Mathf.Cos(a1) * 4.9f, Mathf.Sin(a1) * 4.9f, 0);
                    var mid = (p0 + p1) * 0.5f;
                    mb.For(k % 2 == 0 ? wall : Clad(new Color(0.8f, 0.85f, 0.9f))).BoxRot(mid, new Vector3((p1 - p0).magnitude + 0.02f, 0.15f, 7f), new Vector3(0, 0, Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg));
                }
                mb.For(Mats.Surface(SurfKind.Generic, Q(new Color(0.95f, 0.97f, 1f)), 0.35f)).Sphere(new Vector3(0, 4.6f, 0), 2.6f, 14, 6, 0.25f);
                mb.M = o;
                mb.For(wall).Box(new Vector3(-26f, gy + 2.2f, -149.4f), new Vector3(9.6f, 4.4f, 0.2f));
            }
            else
            {
                mb.For(wall).BevelBox(new Vector3(-26, gy + 2.3f, -146), new Vector3(10, 4.6f, 7), 0.06f);
                var o = mb.M;
                mb.M = Matrix4x4.TRS(new Vector3(-26, gy + 4.6f, -146), Quaternion.Euler(0, 90, 0), Vector3.one);
                mb.For(roof).Prism(Vector3.zero, new Vector3(7.6f, 1.3f, 10.6f));
                mb.M = o;
                if (stationStyle == 2) for (float y = 0.5f; y < 4.6f; y += 0.4f) det.For(Wood(new Color(0.28f, 0.42f, 0.5f))).Box(new Vector3(-26, gy + y, fz + 0.015f), new Vector3(10.05f, 0.04f, 0.03f));
                else for (float x = -30.8f; x < -21f; x += 0.4f) det.For(Paint(new Color(0.4f, 0.22f, 0.14f))).Box(new Vector3(x, gy + 2.3f, fz + 0.02f), new Vector3(0.06f, 4.6f, 0.04f));
            }
            // Tor (dunkle Öffnung mit Sektionaltor), Warnpfosten, Leuchten, Piktogramm
            mb.For(dark).Box(new Vector3(-26, gy + 1.9f, fz + 0.04f), new Vector3(6.6f, 3.8f, 0.06f));
            for (int k = 0; k < 4; k++)
                mb.For(stationStyle == 2 ? Wood(new Color(0.6f, 0.5f, 0.38f)) : Paint(new Color(0.7f, 0.72f, 0.74f))).BevelBox(new Vector3(-26, gy + 0.45f + k * 0.9f, fz + 0.09f), new Vector3(6.4f, 0.84f, 0.05f), 0.02f);
            BaseLamp(mb, new Vector3(-30.0f, gy + 4.0f, fz + 0.02f), Vector3.forward);
            BaseLamp(mb, new Vector3(-22.0f, gy + 4.0f, fz + 0.02f), Vector3.forward);
            IconSign(mb, new Vector3(-26, gy + 4.25f, fz + 0.1f), 0, 0.7f, SurfaceLook.Icon.Rover, stationStyle == 1 ? new Color(1f, 0.5f, 0.2f) : stationStyle == 2 ? new Color(0.2f, 0.75f, 0.8f) : new Color(0.3f, 0.7f, 1f), false);
        }

        // ================================================================== Licht, Rauch, Leuchtfeuer, LOD
        void AnimateStation(float dark)
        {
            float t = Time.time;
            var cam = Camera.main;
            var b = Layout != null ? Layout.Base : null;
            float camDist = cam != null && b != null ? Vector3.Distance(cam.transform.position, new Vector3(b.Center.x, b.Center.y, b.Center.z - 10f)) : 0f;
            // Feindetail nur in der Nähe
            if (baseDetail != null)
            {
                bool want = camDist < 75f;
                if (baseDetail.gameObject.activeSelf != want) baseDetail.gameObject.SetActive(want);
            }
            if (neonMat != null)
            {
                // Leuchtröhren: tagsüber schwach, nachts kräftig, gelegentliches Flackern
                float flick = Mathf.Repeat(t * 0.37f, 1f) < 0.04f ? (Mathf.Sin(t * 90f) > 0f ? 0.3f : 1f) : 1f;
                Mats.SetEmission(neonMat, new Color(1f, 0.3f, 0.55f) * Mathf.Lerp(0.5f, 3.2f, dark) * flick);
            }
            if (ventMat != null) Mats.SetEmission(ventMat, new Color(1.6f, 0.5f, 0.12f) * (0.7f + 0.3f * Mathf.Sin(t * 1.7f)) * Mathf.Lerp(0.8f, 1.6f, dark));
            if (heatMat != null) Mats.SetEmission(heatMat, new Color(1.4f, 0.35f, 0.1f) * (0.85f + 0.15f * Mathf.Sin(t * 2.3f)) * Mathf.Lerp(0.9f, 1.8f, dark));
            if (beaconMat != null)
            {
                if (stationStyle == 2) Mats.SetEmission(beaconMat, new Color(1f, 0.9f, 0.6f) * Mathf.Lerp(0.4f, 4.5f, dark));
                else Mats.SetEmission(beaconMat, new Color(2f, 0.4f, 0.15f) * (Mathf.Repeat(t, 1.6f) < 0.6f ? Mathf.Lerp(1.2f, 3.5f, dark) : 0.2f));
            }
            if (lighthouseBeam != null)
            {
                bool on = dark > 0.2f;
                if (lighthouseBeam.gameObject.activeSelf != on) lighthouseBeam.gameObject.SetActive(on);
                if (on) lighthouseBeam.localRotation = Quaternion.Euler(0, t * 40f, 0);
            }
            // Schornsteinrauch (PYRA) – nur wenn die Kamera nahe genug ist
            if (chimneyTops.Count > 0 && FxView.I != null && camDist < 140f)
            {
                smokeTimer -= Time.deltaTime;
                if (smokeTimer <= 0f)
                {
                    smokeTimer = 0.45f;
                    var top = chimneyTops[Random.Range(0, chimneyTops.Count)];
                    FxView.I.Burst(top, new Color(0.32f, 0.27f, 0.25f, 0.45f), 2, 0.6f, 1.6f, 4.5f, -0.08f);
                    if (Random.value < 0.3f) FxView.I.Burst(top, new Color(1f, 0.55f, 0.2f, 0.8f), 3, 1.4f, 0.12f, 0.9f, 0.3f, true);
                }
            }
            // Kabinenlicht im Hangar: gedimmt, solange jemand darin schläft
            if (b != null && b.Hangar != null)
            {
                bool sleeper = false;
                var w = World;
                if (w != null && ActorsView.I != null)
                    foreach (var p in w.Players.Values)
                        if (p.Online && ActorsView.I.SleepingVisual(p) && b.Hangar.Contains(p.Pos, 0.3f)) { sleeper = true; break; }
                cabinDim = Mathf.MoveTowards(cabinDim, sleeper ? 1f : 0f, Time.deltaTime * 0.5f);
                if (cabinMat != null) Mats.SetEmission(cabinMat, cabinCol * Mathf.Lerp(2.2f, 0.45f, cabinDim));
                if (hangarLight != null) hangarLight.intensity = Mathf.Lerp(1.6f, 0.4f, cabinDim);
            }
        }

        /// <summary>Drehender Lichtstrahl des Leuchtfeuers (PELAGIA, nur nachts sichtbar).</summary>
        void BuildLighthouseBeam(float gy)
        {
            var go = new GameObject("Leuchtfeuer");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = new Vector3(6.1f, gy + 13.55f, -145.45f);
            var mb = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                var a = new Vector3(0, 0, s * 0.4f); var b0 = new Vector3(-1.4f, -0.9f, s * 14f); var c0 = new Vector3(1.4f, 0.9f, s * 14f);
                mb.TriFace(a, b0, c0, Vector3.up); mb.TriFace(a, c0, b0, Vector3.down);
                var d0 = new Vector3(-1.4f, 0.9f, s * 14f); var e0 = new Vector3(1.4f, -0.9f, s * 14f);
                mb.TriFace(a, d0, e0, Vector3.right); mb.TriFace(a, e0, d0, Vector3.left);
            }
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("leuchtfeuer");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Get(Mats.ParticleAdd, new Color(1f, 0.9f, 0.6f, 0.18f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            lighthouseBeam = go.transform;
            go.SetActive(false);
        }
    }
}
