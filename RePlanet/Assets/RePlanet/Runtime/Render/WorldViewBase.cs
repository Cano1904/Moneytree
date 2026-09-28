using RePlanet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RePlanet
{
    /// <summary>
    /// Stützpunkt: Hauptgebäude mit Lager-Silos, Garage, Stationen als erkennbare Maschinen (Verkaufsterminal,
    /// Werkstatt mit Werkzeugwand, Sortieranlage, Marktstand, Entsorgung mit Gefahrstoffzeichen, Auftragstafel,
    /// Ladeplatz mit Kabeln) mit großen, beidseitig lesbaren Schildern – und das Transportschiff als Raumschiff.
    /// Alle Teile unter 2,2 m bleiben innerhalb der Kollisionsbox ihrer Station; Dächer und Schilder ragen darüber hinaus.
    /// </summary>
    public partial class WorldView
    {
        // ------------------------------------------------------------------ Schilder
        /// <summary>Schild mit großer Schrift, von vorn (+Z-Seite des Schilds) und hinten lesbar.</summary>
        void SignBoard(MultiBuilder mb, Vector3 center, float yaw, float w, float h, string text, Color accent, Color textCol, float maxSize = 0.34f)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(center, rot, Vector3.one);
            mb.For(Mat(new Color(0.12f, 0.13f, 0.15f))).Box(Vector3.zero, new Vector3(w, h, 0.12f));
            var acc = Mat(accent);
            mb.For(acc).Box(new Vector3(0, h * 0.5f + 0.05f, 0), new Vector3(w + 0.1f, 0.1f, 0.16f));
            mb.For(acc).Box(new Vector3(0, -h * 0.5f - 0.05f, 0), new Vector3(w + 0.1f, 0.1f, 0.16f));
            mb.For(acc).Box(new Vector3(-w * 0.5f - 0.05f, 0, 0), new Vector3(0.1f, h + 0.2f, 0.16f));
            mb.For(acc).Box(new Vector3(w * 0.5f + 0.05f, 0, 0), new Vector3(0.1f, h + 0.2f, 0.16f));
            mb.M = o;
            float size = Mathf.Min(maxSize, (w * 0.92f) / Mathf.Max(3, text.Length));
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject("Schild_" + text);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = center + rot * new Vector3(0, 0, side == 0 ? 0.08f : -0.08f);
                go.transform.localRotation = rot * Quaternion.Euler(0, side == 0 ? 180 : 0, 0);
                TextLabel(go.transform, text, Vector3.zero, size, textCol);
            }
        }

        static Material Glow(Color c, float k) { return Mats.Get(Mats.Emissive, c, c * k); }

        // ================================================================== Stützpunkt
        void BuildBase()
        {
            var b = Layout.Base;
            var mb = new MultiBuilder();
            if (trimMat == null) InitBuildingMats();
            float gy = b.Center.y;
            CoreBuilding(mb, gy);
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
                var name = StationNames.ContainsKey(kv.Key) ? StationNames[kv.Key] : kv.Key;
                switch (kv.Key)
                {
                    case "sell": SellTerminal(mb, sp, c); break;
                    case "workshop": Workshop(mb, sp, c); break;
                    case "sort": SortingStation(mb, sp, c); break;
                    case "trader": TraderStall(mb, sp, c); break;
                    case "disposal": DisposalStation(mb, sp, c); break;
                    case "contracts": ContractBoard(mb, sp, c); break;
                    default:
                        mb.For(darkMat).Box(new Vector3(sp.x, gy + 1f, sp.z - 1.2f), new Vector3(1.2f, 2f, 0.8f));
                        break;
                }
                // Großes Schild über der Station (Unterkante 2,9 m, frei über dem Roboter)
                SignBoard(mb, new Vector3(sp.x, gy + 3.35f, sp.z - 1.2f), 0, 2.8f, 0.8f, name, c, Color.white);
                mb.For(railMat).Box(new Vector3(sp.x - 0.62f, gy + 2.5f, sp.z - 1.62f), new Vector3(0.08f, 2.0f, 0.08f));
                mb.For(railMat).Box(new Vector3(sp.x + 0.62f, gy + 2.5f, sp.z - 1.62f), new Vector3(0.08f, 2.0f, 0.08f));
                mb.For(railMat).Box(new Vector3(sp.x, gy + 2.95f, sp.z - 1.4f), new Vector3(1.4f, 0.08f, 0.5f));
                // Bodenmarkierung vor der Station
                mb.For(Glow(c, 0.9f)).Box(new Vector3(sp.x, gy + 0.02f, sp.z + 0.1f), new Vector3(1.8f, 0.03f, 0.12f));
            }
            // Lager-Annahme: leuchtende Fläche mit Förderband ins Hauptgebäude
            var st = b.Stations["storage"];
            var teal = Mats.C(0x2EC4B6);
            mb.For(Glow(teal, 1f)).Box(new Vector3(st.x, gy + 0.03f, st.z), new Vector3(5f, 0.05f, 2.5f));
            mb.For(darkMat).Box(new Vector3(st.x, gy + 0.05f, st.z), new Vector3(4.6f, 0.05f, 2.1f));
            for (int i = 0; i < 9; i++) mb.For(railMat).Box(new Vector3(st.x - 2.0f + i * 0.5f, gy + 0.09f, st.z - 0.9f), new Vector3(0.06f, 0.04f, 1.0f));
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(st.x, gy + 0.06f, st.z + 1.1f), new Vector3(4.6f, 0.04f, 0.12f));
            mb.Build("Base", Root, true);
        }

        // ------------------------------------------------------------------ Hauptgebäude mit Lager-Silos
        void CoreBuilding(MultiBuilder mb, float gy)
        {
            var white = Mat(new Color(0.9f, 0.88f, 0.82f));
            var teal = Mat(new Color(0.18f, 0.72f, 0.68f));
            var orange = Mat(new Color(1f, 0.55f, 0.18f));
            const float fz = -141.5f; // Vorderseite (z −149,5 … −141,5)
            // Halle (x −8 … 3.6)
            mb.For(white).Box(new Vector3(-2.2f, gy + 3.5f, -145.5f), new Vector3(11.6f, 7f, 8f));
            mb.For(plinthMat).Box(new Vector3(-2.2f, gy + 0.4f, -145.5f), new Vector3(11.8f, 0.8f, 8.15f));
            mb.For(teal).Box(new Vector3(-2.2f, gy + 7.2f, -145.5f), new Vector3(12.2f, 0.5f, 8.6f));
            mb.For(orange).Box(new Vector3(-2.2f, gy + 2.6f, fz + 0.03f), new Vector3(11.6f, 0.35f, 0.08f));
            // Rolltor hinter der Lager-Annahme (halb offen, innen beleuchtet)
            mb.For(darkMat).Box(new Vector3(0, gy + 2.1f, fz + 0.02f), new Vector3(4.6f, 4.2f, 0.06f));
            mb.For(Glow(new Color(1f, 0.85f, 0.6f), 0.8f)).Box(new Vector3(0, gy + 0.7f, fz - 0.05f), new Vector3(4.4f, 1.2f, 0.04f));
            mb.For(acMat).Box(new Vector3(0, gy + 3.3f, fz + 0.08f), new Vector3(4.6f, 1.9f, 0.08f));
            for (int k = 0; k < 6; k++) mb.For(railMat).Box(new Vector3(0, gy + 2.45f + k * 0.34f, fz + 0.13f), new Vector3(4.6f, 0.04f, 0.03f));
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(0, gy + 4.35f, fz + 0.1f), new Vector3(5f, 0.2f, 0.12f));
            // Fensterreihe oben und Tür links
            foreach (var x in new[] { -4.0f, 2.4f })
            {
                mb.For(WindowMat(0)).Box(new Vector3(x, gy + 5.05f, fz + 0.03f), new Vector3(1.3f, 1.0f, 0.06f));
                mb.For(trimMat).Box(new Vector3(x, gy + 4.5f, fz + 0.1f), new Vector3(1.5f, 0.08f, 0.2f));
            }
            mb.For(darkMat).Box(new Vector3(-4.2f, gy + 1.15f, fz + 0.03f), new Vector3(1.1f, 2.3f, 0.06f));
            mb.For(teal).Box(new Vector3(-4.2f, gy + 2.55f, fz + 0.45f), new Vector3(1.8f, 0.1f, 0.9f));
            // großer Bildschirm (Symbol) und Schild
            mb.For(Glow(new Color(0.3f, 1f, 0.8f), 1.6f)).Box(new Vector3(-6.4f, gy + 5.3f, fz + 0.04f), new Vector3(2.2f, 1.3f, 0.06f));
            mb.For(darkMat).Box(new Vector3(-6.4f, gy + 5.3f, fz + 0.08f), new Vector3(0.9f, 0.9f, 0.02f));
            SignBoard(mb, new Vector3(-0.6f, gy + 6.35f, fz + 0.12f), 0, 8.6f, 0.95f, "RECYCLING-STÜTZPUNKT", new Color(0.18f, 0.72f, 0.68f), Color.white, 0.44f);
            // Dach: Solarmodule, Antennenmast, Klimageräte, Warnleuchte
            for (int i = 0; i < 3; i++)
                mb.For(Mats.Get(Mats.Opaque, new Color(0.14f, 0.2f, 0.42f), null, 0.9f)).BoxRot(new Vector3(-6f + i * 2.6f, gy + 7.9f, -146.5f), new Vector3(2.3f, 0.08f, 3.6f), new Vector3(-22, 0, 0));
            mb.For(railMat).Cylinder(new Vector3(1.8f, gy + 7.4f, -148f), 0.12f, 6f, 6);
            var o = mb.M;
            mb.M = Matrix4x4.TRS(new Vector3(1.8f, gy + 12.6f, -148f), Quaternion.Euler(-50, 150, 0), Vector3.one);
            mb.For(acMat).Lathe(Vector3.zero, new[] { new Vector2(0, 0), new Vector2(0.8f, 0.25f), new Vector2(1.2f, 0.55f) }, 14);
            mb.M = o;
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(1.8f, gy + 13.5f, -148f), 0.2f, 6, 4);
            mb.For(acMat).Box(new Vector3(-1.5f, gy + 7.9f, -143.5f), new Vector3(1.6f, 0.9f, 1.2f));
            mb.For(darkMat).Cylinder(new Vector3(-1.5f, gy + 8.35f, -143.5f), 0.45f, 0.04f, 10);
            // Lager-Silos (x 4.3 … 7.9) mit Kegeldach, Ringen, Leiter und Rohrbrücke
            for (int s = 0; s < 2; s++)
            {
                var p = new Vector3(6.1f, gy, s == 0 ? -143.5f : -147.4f);
                mb.For(white).Cylinder(p, 1.75f, 10f, 20, false);
                mb.For(teal).Cylinder(p + Vector3.up * 10f, 1.8f, 1.6f, 20, true, 0.3f);
                for (float y = 1.5f; y < 10f; y += 2.2f) mb.For(railMat).Torus(p + Vector3.up * y, 1.77f, 0.07f, 20, 4);
                mb.For(orange).Cylinder(p + Vector3.up * 6.5f, 1.78f, 0.6f, 20, false);
                mb.For(railMat).Tube(p + new Vector3(-1.2f, 9.2f, 0), new Vector3(3.2f, gy + 7.6f, p.z), 0.25f, 8);
            }
            mb.For(railMat).Box(new Vector3(7.95f, gy + 5f, -143.5f), new Vector3(0.06f, 10f, 0.06f));
            mb.For(railMat).Box(new Vector3(7.95f, gy + 5f, -142.9f), new Vector3(0.06f, 10f, 0.06f));
            for (float y = 0.4f; y < 10f; y += 0.4f) mb.For(railMat).Box(new Vector3(7.95f, gy + y, -143.2f), new Vector3(0.04f, 0.04f, 0.6f));
            SignBoard(mb, new Vector3(6.1f, gy + 3.2f, -141.62f), 0, 2.4f, 0.7f, "LAGER", new Color(0.18f, 0.77f, 0.71f), Color.white);
        }

        // ------------------------------------------------------------------ Garage
        void GarageBuilding(MultiBuilder mb, float gy)
        {
            var wall = Mat(new Color(0.72f, 0.68f, 0.6f));
            var orange = Mat(new Color(1f, 0.55f, 0.18f));
            float fz = -142.5f;
            mb.For(wall).Box(new Vector3(-26, gy + 2.5f, -146), new Vector3(10, 5, 7));
            mb.For(plinthMat).Box(new Vector3(-26, gy + 0.35f, -146), new Vector3(10.15f, 0.7f, 7.15f));
            mb.For(orange).Box(new Vector3(-26, gy + 5.2f, -146), new Vector3(10.4f, 0.4f, 7.4f));
            // Sektionaltor mit Paneelen und Fenstern
            mb.For(darkMat).Box(new Vector3(-26, gy + 2f, fz + 0.02f), new Vector3(7.2f, 4.1f, 0.06f));
            for (int k = 0; k < 5; k++)
            {
                float y = gy + 0.4f + k * 0.8f;
                mb.For(acMat).Box(new Vector3(-26, y, fz + 0.07f), new Vector3(7f, 0.74f, 0.06f));
                if (k == 3) for (int i = 0; i < 5; i++) mb.For(WindowMat(0)).Box(new Vector3(-28.8f + i * 1.4f, y, fz + 0.11f), new Vector3(0.9f, 0.35f, 0.02f));
            }
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(-29.7f, gy + 2f, fz + 0.1f), new Vector3(0.18f, 4f, 0.1f));
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(-22.3f, gy + 2f, fz + 0.1f), new Vector3(0.18f, 4f, 0.1f));
            for (int k = 0; k < 6; k++) mb.For(darkMat).Box(new Vector3(-29.7f, gy + 0.3f + k * 0.7f, fz + 0.16f), new Vector3(0.19f, 0.3f, 0.02f));
            for (int k = 0; k < 6; k++) mb.For(darkMat).Box(new Vector3(-22.3f, gy + 0.3f + k * 0.7f, fz + 0.16f), new Vector3(0.19f, 0.3f, 0.02f));
            // Wandlampen und Schild
            mb.For(LampMat(0)).Box(new Vector3(-30.3f, gy + 4.3f, fz + 0.2f), new Vector3(0.4f, 0.2f, 0.3f));
            mb.For(LampMat(0)).Box(new Vector3(-21.7f, gy + 4.3f, fz + 0.2f), new Vector3(0.4f, 0.2f, 0.3f));
            SignBoard(mb, new Vector3(-26, gy + 4.6f, fz + 0.12f), 0, 3.2f, 0.6f, "GARAGE", new Color(1f, 0.55f, 0.18f), Color.white);
            // Seitenfenster, Dachlüfter
            mb.For(WindowMat(0)).Box(new Vector3(-31.03f, gy + 2.8f, -146f), new Vector3(0.06f, 1f, 2.2f));
            mb.For(acMat).Cylinder(new Vector3(-24f, gy + 5.4f, -147f), 0.4f, 0.8f, 8);
            mb.For(acMat).Cylinder(new Vector3(-24f, gy + 6.2f, -147f), 0.6f, 0.2f, 8, true, 0.1f);
        }

        // ------------------------------------------------------------------ Stationen
        void SellTerminal(MultiBuilder mb, Vector3 sp, Color c)
        {
            var body = Mat(new Color(0.2f, 0.22f, 0.25f));
            var accent = Mat(c);
            var cz = sp.z - 1.2f;
            mb.For(body).Box(new Vector3(sp.x, sp.y + 1.05f, cz), new Vector3(1.3f, 2.1f, 0.9f));
            mb.For(accent).Box(new Vector3(sp.x, sp.y + 2.12f, cz), new Vector3(1.36f, 0.1f, 0.96f));
            // schräger Bildschirm mit Kurs-Balken
            mb.For(Glow(c, 1.6f)).BoxRot(new Vector3(sp.x, sp.y + 1.55f, cz + 0.47f), new Vector3(1.0f, 0.62f, 0.04f), new Vector3(-12, 0, 0));
            for (int k = 0; k < 4; k++) mb.For(darkMat).BoxRot(new Vector3(sp.x - 0.3f + k * 0.2f, sp.y + 1.45f + k * 0.04f, cz + 0.5f), new Vector3(0.12f, 0.12f + k * 0.08f, 0.01f), new Vector3(-12, 0, 0));
            // Münzschlitz, Tastenfeld, Ausgabefach, Leuchtstreifen
            mb.For(acMat).Box(new Vector3(sp.x + 0.35f, sp.y + 1.05f, cz + 0.46f), new Vector3(0.3f, 0.4f, 0.04f));
            for (int k = 0; k < 9; k++) mb.For(signLight).Box(new Vector3(sp.x + 0.27f + (k % 3) * 0.08f, sp.y + 0.95f + (k / 3) * 0.08f, cz + 0.485f), new Vector3(0.05f, 0.05f, 0.01f));
            mb.For(darkMat).Box(new Vector3(sp.x - 0.25f, sp.y + 0.6f, cz + 0.46f), new Vector3(0.6f, 0.25f, 0.04f));
            mb.For(Glow(c, 2f)).Box(new Vector3(sp.x - 0.66f, sp.y + 1.05f, cz + 0.2f), new Vector3(0.03f, 1.8f, 0.3f));
            mb.For(Glow(c, 2f)).Box(new Vector3(sp.x + 0.66f, sp.y + 1.05f, cz + 0.2f), new Vector3(0.03f, 1.8f, 0.3f));
            // Credit-Symbol oben
            mb.For(Glow(new Color(1f, 0.8f, 0.3f), 1.8f)).TorusRot(new Vector3(sp.x, sp.y + 2.55f, cz), new Vector3(90, 0, 0), 0.22f, 0.05f, 14, 4);
        }

        void Workshop(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Mat(c);
            var wood = woodMat;
            // Rückwand mit Lochblech und Werkzeugen
            mb.For(Mat(new Color(0.3f, 0.32f, 0.35f))).Box(new Vector3(sp.x, sp.y + 1.25f, cz - 0.42f), new Vector3(1.4f, 2.5f, 0.16f));
            mb.For(Mat(new Color(0.75f, 0.72f, 0.62f))).Box(new Vector3(sp.x, sp.y + 1.6f, cz - 0.33f), new Vector3(1.3f, 1.0f, 0.02f));
            var tool = MetalMat(new Color(0.75f, 0.77f, 0.8f));
            mb.For(tool).BoxRot(new Vector3(sp.x - 0.45f, sp.y + 1.65f, cz - 0.31f), new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0, 0, 8));
            mb.For(tool).TorusRot(new Vector3(sp.x - 0.43f, sp.y + 1.95f, cz - 0.31f), new Vector3(90, 0, 0), 0.07f, 0.02f, 8, 3);
            mb.For(accent).Box(new Vector3(sp.x - 0.18f, sp.y + 1.45f, cz - 0.31f), new Vector3(0.06f, 0.3f, 0.03f));
            mb.For(tool).Box(new Vector3(sp.x - 0.18f, sp.y + 1.72f, cz - 0.31f), new Vector3(0.22f, 0.1f, 0.04f));
            mb.For(Mat(new Color(0.85f, 0.2f, 0.18f))).Box(new Vector3(sp.x + 0.12f, sp.y + 1.5f, cz - 0.31f), new Vector3(0.05f, 0.35f, 0.03f));
            mb.For(tool).BoxRot(new Vector3(sp.x + 0.12f, sp.y + 1.75f, cz - 0.31f), new Vector3(0.06f, 0.2f, 0.03f), new Vector3(0, 0, 90));
            for (int k = 0; k < 4; k++) mb.For(tool).Box(new Vector3(sp.x + 0.36f + k * 0.07f, sp.y + 1.55f - k * 0.03f, cz - 0.31f), new Vector3(0.03f, 0.3f + k * 0.05f, 0.02f));
            // Werkbank mit Schraubstock und roter Werkzeugkiste
            mb.For(wood).Box(new Vector3(sp.x, sp.y + 0.95f, cz + 0.05f), new Vector3(1.4f, 0.08f, 0.8f));
            mb.For(railMat).Box(new Vector3(sp.x - 0.62f, sp.y + 0.46f, cz + 0.05f), new Vector3(0.08f, 0.92f, 0.7f));
            mb.For(railMat).Box(new Vector3(sp.x + 0.62f, sp.y + 0.46f, cz + 0.05f), new Vector3(0.08f, 0.92f, 0.7f));
            mb.For(Mat(new Color(0.8f, 0.18f, 0.15f))).Box(new Vector3(sp.x + 0.2f, sp.y + 0.45f, cz - 0.05f), new Vector3(0.7f, 0.8f, 0.5f));
            for (int k = 0; k < 4; k++) mb.For(acMat).Box(new Vector3(sp.x + 0.2f, sp.y + 0.2f + k * 0.18f, cz + 0.21f), new Vector3(0.6f, 0.03f, 0.02f));
            mb.For(tool).Box(new Vector3(sp.x - 0.4f, sp.y + 1.08f, cz + 0.3f), new Vector3(0.22f, 0.18f, 0.2f));
            mb.For(tool).Box(new Vector3(sp.x - 0.4f, sp.y + 1.12f, cz + 0.44f), new Vector3(0.05f, 0.05f, 0.25f));
            mb.For(accent).Crumple(new Vector3(sp.x + 0.35f, sp.y + 1.05f, cz + 0.25f), 0.1f, 0.6f, 3, 0.2f, 6, 3);
            // Garagentor-Front als Dach (über 2,4 m): Rolltor-Kasten mit Lamellen und Warnstreifen
            mb.For(acMat).Box(new Vector3(sp.x, sp.y + 2.55f, cz - 0.1f), new Vector3(2.6f, 0.4f, 0.9f));
            for (int k = 0; k < 3; k++) mb.For(railMat).Box(new Vector3(sp.x, sp.y + 2.42f + k * 0.12f, cz + 0.36f), new Vector3(2.6f, 0.03f, 0.02f));
            for (int k = 0; k < 7; k++) mb.For(k % 2 == 0 ? Mat(new Color(0.95f, 0.75f, 0.15f)) : darkMat).BoxRot(new Vector3(sp.x - 1.1f + k * 0.37f, sp.y + 2.3f, cz + 0.37f), new Vector3(0.3f, 0.12f, 0.02f), new Vector3(0, 0, 30));
            mb.For(LampMat(0)).Box(new Vector3(sp.x, sp.y + 2.3f, cz + 0.1f), new Vector3(1.0f, 0.06f, 0.2f));
        }

        void SortingStation(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Mat(c);
            // Rahmen, schräges Sortierband, drei farbige Behälter
            mb.For(railMat).Box(new Vector3(sp.x - 0.64f, sp.y + 0.75f, cz), new Vector3(0.08f, 1.5f, 0.9f));
            mb.For(railMat).Box(new Vector3(sp.x + 0.64f, sp.y + 0.75f, cz), new Vector3(0.08f, 1.5f, 0.9f));
            mb.For(Mat(new Color(0.12f, 0.12f, 0.13f))).BoxRot(new Vector3(sp.x, sp.y + 1.25f, cz - 0.1f), new Vector3(1.2f, 0.06f, 0.8f), new Vector3(-18, 0, 0));
            for (int k = 0; k < 5; k++) mb.For(acMat).CylinderX(new Vector3(sp.x, sp.y + 1.22f + (k - 2) * 0.05f, cz - 0.1f - (k - 2) * 0.15f), 0.04f, 1.2f, 6);
            var bins = new[] { new Color(0.25f, 0.55f, 0.85f), new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.7f, 0.35f) };
            for (int k = 0; k < 3; k++)
            {
                var p = new Vector3(sp.x - 0.42f + k * 0.42f, sp.y, cz + 0.25f);
                mb.For(Mat(bins[k])).BoxNoBottom(p + Vector3.up * 0.3f, new Vector3(0.38f, 0.6f, 0.38f));
                mb.For(darkMat).Box(p + Vector3.up * 0.58f, new Vector3(0.32f, 0.02f, 0.32f));
                mb.For(signLight).Box(p + new Vector3(0, 0.35f, 0.195f), new Vector3(0.16f, 0.16f, 0.01f));
            }
            // Trichter oben mit Leuchtring
            mb.For(accent).Cylinder(new Vector3(sp.x, sp.y + 1.55f, cz - 0.15f), 0.18f, 0.7f, 12, false, 0.55f);
            mb.For(Glow(c, 1.8f)).Torus(new Vector3(sp.x, sp.y + 2.25f, cz - 0.15f), 0.56f, 0.04f, 16, 4);
            mb.For(Glow(c, 1.4f)).Box(new Vector3(sp.x, sp.y + 1.6f, cz + 0.46f), new Vector3(0.5f, 0.2f, 0.02f));
        }

        void TraderStall(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var accent = Mat(c);
            // Theke mit Waren
            mb.For(woodMat).Box(new Vector3(sp.x, sp.y + 0.5f, cz + 0.1f), new Vector3(1.4f, 1.0f, 0.7f));
            mb.For(accent).Box(new Vector3(sp.x, sp.y + 1.02f, cz + 0.1f), new Vector3(1.45f, 0.06f, 0.76f));
            var goods = new[] { new Color(0.72f, 0.45f, 0.25f), new Color(0.6f, 0.75f, 0.85f), new Color(0.35f, 0.65f, 0.4f), new Color(0.8f, 0.8f, 0.82f) };
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector3(sp.x - 0.5f + k * 0.33f, sp.y + 1.15f, cz + 0.05f);
                if (k % 2 == 0) mb.For(Mat(goods[k])).BoxJ(p, new Vector3(0.24f, 0.2f, 0.24f), new Vector3(0, k * 20, 0), 0.02f, k);
                else mb.For(MetalMat(goods[k])).CylinderX(p, 0.08f, 0.26f, 8);
            }
            // Kisten hinter der Theke, Pfosten und gestreifte Markise
            mb.For(woodMat).Box(new Vector3(sp.x - 0.35f, sp.y + 0.3f, cz - 0.38f), new Vector3(0.6f, 0.6f, 0.2f));
            mb.For(railMat).Box(new Vector3(sp.x - 0.66f, sp.y + 1.3f, cz - 0.45f), new Vector3(0.07f, 2.6f, 0.07f));
            mb.For(railMat).Box(new Vector3(sp.x + 0.66f, sp.y + 1.3f, cz - 0.45f), new Vector3(0.07f, 2.6f, 0.07f));
            for (int k = 0; k < 6; k++)
                mb.For(k % 2 == 0 ? accent : signLight).BoxRot(new Vector3(sp.x - 1.25f + k * 0.5f, sp.y + 2.55f, cz + 0.25f), new Vector3(0.5f, 0.05f, 1.6f), new Vector3(-14, 0, 0));
            mb.For(LampMat(0)).Sphere(new Vector3(sp.x + 0.5f, sp.y + 2.25f, cz + 0.4f), 0.12f, 8, 5);
        }

        void DisposalStation(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            var yellow = Mat(new Color(0.95f, 0.75f, 0.15f));
            // Gefahrstoff-Container mit Warnstreifen und Klappdeckel
            mb.For(yellow).Box(new Vector3(sp.x, sp.y + 0.65f, cz), new Vector3(1.35f, 1.3f, 0.95f));
            for (int k = 0; k < 6; k++) mb.For(darkMat).BoxRot(new Vector3(sp.x - 0.55f + k * 0.22f, sp.y + 0.2f, cz + 0.48f), new Vector3(0.1f, 0.35f, 0.02f), new Vector3(0, 0, 35));
            mb.For(Mat(new Color(0.85f, 0.25f, 0.18f))).BoxRot(new Vector3(sp.x, sp.y + 1.42f, cz - 0.1f), new Vector3(1.38f, 0.08f, 0.98f), new Vector3(-12, 0, 0));
            mb.For(acMat).Box(new Vector3(sp.x, sp.y + 1.1f, cz + 0.49f), new Vector3(0.6f, 0.08f, 0.04f));
            // Gefahrstoffzeichen (Raute) auf der Front und groß auf dem Pfosten
            HazardDiamond(mb, new Vector3(sp.x + 0.35f, sp.y + 0.8f, cz + 0.48f), 0.32f);
            mb.For(railMat).Box(new Vector3(sp.x - 0.55f, sp.y + 1.9f, cz - 0.3f), new Vector3(0.07f, 1.2f, 0.07f));
            HazardDiamond(mb, new Vector3(sp.x - 0.55f, sp.y + 2.35f, cz - 0.25f), 0.55f);
            mb.For(Glow(c, 1.6f)).Box(new Vector3(sp.x, sp.y + 1.2f, cz + 0.49f), new Vector3(1.2f, 0.06f, 0.02f));
        }

        void HazardDiamond(MultiBuilder mb, Vector3 p, float s)
        {
            mb.For(Mat(new Color(0.85f, 0.15f, 0.12f))).BoxRot(p, new Vector3(s, s, 0.03f), new Vector3(0, 0, 45));
            mb.For(signLight).BoxRot(p + new Vector3(0, 0, 0.02f), new Vector3(s * 0.78f, s * 0.78f, 0.02f), new Vector3(0, 0, 45));
            mb.For(darkMat).Box(p + new Vector3(0, s * 0.12f, 0.035f), new Vector3(s * 0.12f, s * 0.42f, 0.01f));
            mb.For(darkMat).Box(p + new Vector3(0, -s * 0.28f, 0.035f), new Vector3(s * 0.12f, s * 0.12f, 0.01f));
        }

        void ContractBoard(MultiBuilder mb, Vector3 sp, Color c)
        {
            var cz = sp.z - 1.2f;
            mb.For(woodMat).Box(new Vector3(sp.x - 0.62f, sp.y + 1.1f, cz), new Vector3(0.1f, 2.2f, 0.1f));
            mb.For(woodMat).Box(new Vector3(sp.x + 0.62f, sp.y + 1.1f, cz), new Vector3(0.1f, 2.2f, 0.1f));
            mb.For(Mat(new Color(0.55f, 0.4f, 0.28f))).Box(new Vector3(sp.x, sp.y + 1.55f, cz), new Vector3(1.3f, 1.05f, 0.08f));
            mb.For(Mat(c)).Box(new Vector3(sp.x, sp.y + 2.12f, cz + 0.05f), new Vector3(1.45f, 0.12f, 0.3f));
            var papers = new[] { signLight, Mat(new Color(0.98f, 0.9f, 0.55f)), Mat(new Color(0.7f, 0.85f, 0.95f)) };
            for (int k = 0; k < 7; k++)
            {
                var p = new Vector3(sp.x - 0.45f + (k % 4) * 0.3f + (k / 4) * 0.12f, sp.y + 1.78f - (k / 4) * 0.45f, cz + 0.05f);
                mb.For(papers[k % 3]).BoxRot(p, new Vector3(0.22f, 0.3f, 0.01f), new Vector3(0, 0, (k * 7 % 11) - 5));
                mb.For(Mat(new Color(0.85f, 0.2f, 0.18f))).Box(p + new Vector3(0, 0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.02f));
                for (int l = 0; l < 3; l++) mb.For(darkMat).BoxRot(p + new Vector3(0, 0.05f - l * 0.06f, 0.008f), new Vector3(0.15f, 0.012f, 0.005f), new Vector3(0, 0, (k * 7 % 11) - 5));
            }
            mb.For(Glow(c, 1.6f)).Box(new Vector3(sp.x, sp.y + 1.0f, cz + 0.05f), new Vector3(1.2f, 0.05f, 0.02f));
        }

        void ChargeStation(MultiBuilder mb, Vector3 p)
        {
            var green = new Color(0.5f, 1f, 0.3f);
            // Ladefläche mit Ring und Pfeilen
            mb.For(darkMat).Cylinder(p + Vector3.up * 0.01f, 2.4f, 0.04f, 24);
            mb.For(Glow(green, 1.6f)).Torus(p + Vector3.up * 0.05f, 2.5f, 0.06f, 32, 4);
            for (int k = 0; k < 3; k++) mb.For(Glow(green, 1.2f)).BoxRot(p + new Vector3(0, 0.06f, -0.9f + k * 0.6f), new Vector3(0.9f, 0.02f, 0.14f), new Vector3(0, 0, 0));
            // Ladesäule an der Wand des Hauptgebäudes, Kabeltrommel, Kabel am Boden
            var post = new Vector3(p.x, p.y, -141.3f);
            mb.For(Mat(new Color(0.9f, 0.88f, 0.82f))).Box(post + Vector3.up * 1.1f, new Vector3(0.7f, 2.2f, 0.45f));
            mb.For(Glow(green, 1.4f)).Box(post + new Vector3(0, 1.55f, 0.23f), new Vector3(0.45f, 0.35f, 0.02f));
            mb.For(darkMat).BoxRot(post + new Vector3(0, 1.58f, 0.25f), new Vector3(0.08f, 0.22f, 0.01f), new Vector3(0, 0, 25));
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(post + new Vector3(0, 2.25f, 0), new Vector3(0.75f, 0.1f, 0.5f));
            mb.For(darkMat).CylinderZ(post + new Vector3(0, 0.8f, 0.3f), 0.25f, 0.18f, 12);
            var cable = Mat(new Color(0.1f, 0.1f, 0.1f));
            var a = post + new Vector3(0.1f, 0.8f, 0.4f);
            var pts = new[] { a, post + new Vector3(0.2f, 0.05f, 0.7f), p + new Vector3(0.9f, 0.05f, -1.2f), p + new Vector3(0.3f, 0.05f, -0.5f), p + new Vector3(0.2f, 0.06f, 0.2f) };
            for (int k = 0; k < pts.Length - 1; k++) mb.For(cable).Tube(pts[k], pts[k + 1], 0.05f, 6);
            var pts2 = new[] { post + new Vector3(-0.2f, 0.8f, 0.3f), post + new Vector3(-0.35f, 0.05f, 0.6f), p + new Vector3(-1.1f, 0.05f, -1.0f), p + new Vector3(-0.6f, 0.05f, 0.4f) };
            for (int k = 0; k < pts2.Length - 1; k++) mb.For(Mat(new Color(0.95f, 0.5f, 0.15f))).Tube(pts2[k], pts2[k + 1], 0.04f, 6);
            mb.For(acMat).Box(p + new Vector3(0.2f, 0.1f, 0.25f), new Vector3(0.18f, 0.12f, 0.25f));
        }

        void LandingPad(MultiBuilder mb, Vector3 p)
        {
            mb.For(darkMat).Cylinder(p, 6.5f, 0.15f, 32);
            mb.For(Mat(new Color(0.95f, 0.8f, 0.2f))).Torus(p + Vector3.up * 0.16f, 5.8f, 0.12f, 32, 4);
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                mb.For(Glow(k % 2 == 0 ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.7f, 0.3f), 2.2f)).Box(p + new Vector3(Mathf.Cos(a) * 6.2f, 0.2f, Mathf.Sin(a) * 6.2f), new Vector3(0.22f, 0.1f, 0.22f));
            }
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0, k * 90 + 15, 0);
                mb.For(Mat(new Color(0.95f, 0.8f, 0.2f))).BoxRot(p + d * new Vector3(0, 0.16f, 4.4f), new Vector3(1.2f, 0.02f, 0.25f), new Vector3(0, k * 90 + 15, 0));
            }
        }

        // ------------------------------------------------------------------ Transportschiff
        /// <summary>
        /// Frachtraumschiff: abgeflachter Rumpf mit Nase und Cockpitkanzel, Stummelflügel mit Triebwerksgondeln,
        /// zwei Haupttriebwerke mit Leuchtringen, vier Landebeine mit Kolben und Tellern, abgesenkte Laderampe mit
        /// beleuchtetem Frachtraum, Positionslichter, Antenne, Wärmetauscher-Rippen und Akzentstreifen.
        /// Der Rumpf hängt hoch genug, dass MIKO darunter durchpasst.
        /// </summary>
        void BuildShip(Vector3 at)
        {
            var mb = new MultiBuilder();
            var hull = Mats.Get(Mats.Metal, new Color(0.86f, 0.87f, 0.9f));
            var hullDark = Mat(new Color(0.34f, 0.36f, 0.4f));
            var dark = Mat(new Color(0.16f, 0.17f, 0.2f));
            var accent = Mat(new Color(1f, 0.55f, 0.18f));
            var teal = Mat(new Color(0.18f, 0.72f, 0.68f));
            var glass = Mats.Get(Mats.Emissive, new Color(0.1f, 0.2f, 0.28f), new Color(0.1f, 0.35f, 0.5f));
            var engineGlow = Glow(new Color(0.4f, 0.85f, 1f), 2.6f);
            float bodyY = 3.6f;
            var yaw = Quaternion.Euler(0, -30, 0);
            mb.M = Matrix4x4.TRS(at + yaw * new Vector3(0, 0, 1.4f), yaw, Vector3.one);
            var root = mb.M;
            // Rumpf (entlang +Z, Nase vorn): Rotationskörper, abgeflacht
            mb.M = root * Matrix4x4.TRS(new Vector3(0, bodyY, -5.2f), Quaternion.Euler(90, 0, 0), new Vector3(1f, 1f, 0.68f));
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(1.9f, 0.05f), new Vector2(2.3f, 0.8f), new Vector2(2.35f, 6.5f), new Vector2(2.1f, 8.5f), new Vector2(1.5f, 9.8f), new Vector2(0.7f, 10.6f), new Vector2(0f, 10.9f) }, 20);
            mb.M = root;
            // Rückwand, Frachtraum und Laderampe
            mb.For(dark).Box(new Vector3(0, bodyY - 0.2f, -5.2f), new Vector3(2.6f, 1.9f, 0.1f));
            mb.For(Glow(new Color(1f, 0.85f, 0.6f), 1.1f)).Box(new Vector3(0, bodyY + 0.65f, -5.1f), new Vector3(2.2f, 0.08f, 0.2f));
            mb.For(hullDark).BoxRot(new Vector3(0, (bodyY - 1.2f) * 0.5f + 0.05f, -5.2f - 1.9f), new Vector3(2.4f, 0.12f, Mathf.Sqrt(3.8f * 3.8f + (bodyY - 1.2f) * (bodyY - 1.2f))), new Vector3(-Mathf.Atan2(bodyY - 1.2f, 3.8f) * Mathf.Rad2Deg, 0, 0));
            for (int s = -1; s <= 1; s += 2)
                mb.For(accent).BoxRot(new Vector3(s * 1.15f, (bodyY - 1.2f) * 0.5f + 0.18f, -5.2f - 1.9f), new Vector3(0.08f, 0.14f, 4.4f), new Vector3(-Mathf.Atan2(bodyY - 1.2f, 3.8f) * Mathf.Rad2Deg, 0, 0));
            for (int k = 0; k < 6; k++) mb.For(Mat(new Color(0.95f, 0.8f, 0.2f))).BoxRot(new Vector3(0, (bodyY - 1.2f) * (0.12f + k * 0.15f) + 0.1f, -5.2f - 3.6f + k * 0.62f), new Vector3(1.6f, 0.02f, 0.12f), new Vector3(-Mathf.Atan2(bodyY - 1.2f, 3.8f) * Mathf.Rad2Deg, 0, 0));
            // Cockpitkanzel
            mb.M = root * Matrix4x4.TRS(new Vector3(0, bodyY + 1.05f, 3.2f), Quaternion.Euler(-8, 0, 0), new Vector3(1f, 0.55f, 1.6f));
            mb.For(glass).Sphere(Vector3.zero, 1.2f, 16, 8);
            mb.M = root;
            // Akzentstreifen, Plattenfugen, Kennzeichnung
            mb.For(accent).Box(new Vector3(2.32f, bodyY + 0.2f, -0.3f), new Vector3(0.06f, 0.35f, 6.4f));
            mb.For(accent).Box(new Vector3(-2.32f, bodyY + 0.2f, -0.3f), new Vector3(0.06f, 0.35f, 6.4f));
            mb.For(teal).Box(new Vector3(2.33f, bodyY - 0.25f, -0.3f), new Vector3(0.05f, 0.15f, 6.4f));
            mb.For(teal).Box(new Vector3(-2.33f, bodyY - 0.25f, -0.3f), new Vector3(0.05f, 0.15f, 6.4f));
            // Plattenfugen als Ringe um den abgeflachten Rumpf
            for (int k = 0; k < 3; k++)
            {
                mb.M = root * Matrix4x4.TRS(new Vector3(0, bodyY, -3.2f + k * 2.4f), Quaternion.identity, new Vector3(1f, 0.68f, 1f));
                mb.For(hullDark).TorusRot(Vector3.zero, new Vector3(90, 0, 0), 2.36f, 0.035f, 24, 3);
            }
            mb.M = root;
            // Wärmetauscher-Rippen und Antenne auf dem Rücken
            for (int k = 0; k < 6; k++) mb.For(dark).Box(new Vector3(0, bodyY + 1.62f, -3.8f + k * 0.35f), new Vector3(1.4f, 0.3f, 0.06f));
            mb.For(hullDark).Cylinder(new Vector3(0.6f, bodyY + 1.55f, 0.8f), 0.05f, 1.6f, 5);
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(0.6f, bodyY + 3.2f, 0.8f), 0.1f, 6, 4);
            // Stummelflügel mit Triebwerksgondeln
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(hull).BoxRot(new Vector3(s * 3.3f, bodyY - 0.2f, -1.6f), new Vector3(2.6f, 0.28f, 3.4f), new Vector3(0, s * -8, s * -6));
                mb.For(accent).BoxRot(new Vector3(s * 3.9f, bodyY - 0.06f, -0.2f), new Vector3(1.2f, 0.06f, 0.5f), new Vector3(0, s * -8, s * -6));
                var pod = new Vector3(s * 4.6f, bodyY - 0.45f, -2.0f);
                mb.For(hullDark).CylinderZ(pod, 0.62f, 3.6f, 14);
                mb.For(dark).CylinderZ(pod + new Vector3(0, 0, -1.95f), 0.5f, 0.35f, 14);
                mb.For(engineGlow).CylinderZ(pod + new Vector3(0, 0, -2.14f), 0.36f, 0.04f, 14);
                mb.For(accent).CylinderZ(pod + new Vector3(0, 0, 1.4f), 0.64f, 0.3f, 14);
                mb.For(Glow(s < 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f), 3f)).Sphere(pod + new Vector3(s * 0.65f, 0, 0.5f), 0.12f, 6, 4);
            }
            // Haupttriebwerke hinten
            for (int s = -1; s <= 1; s += 2)
            {
                var n = new Vector3(s * 1.1f, bodyY + 0.2f, -5.4f);
                mb.For(dark).CylinderZ(n + new Vector3(0, 0, -0.35f), 0.75f, 0.7f, 16);
                mb.For(hullDark).CylinderZ(n + new Vector3(0, 0, -0.1f), 0.82f, 0.25f, 16);
                mb.For(engineGlow).CylinderZ(n + new Vector3(0, 0, -0.72f), 0.55f, 0.04f, 16);
                mb.For(Glow(new Color(0.4f, 0.85f, 1f), 1.4f)).TorusRot(n + new Vector3(0, 0, -0.7f), new Vector3(90, 0, 0), 0.66f, 0.05f, 16, 4);
            }
            // Landebeine mit Kolben und Tellern (Fuß auf dem Landeplatz)
            var legs = new[] { new Vector3(-2.2f, 0, 2.6f), new Vector3(2.2f, 0, 2.6f), new Vector3(-2.6f, 0, -3.4f), new Vector3(2.6f, 0, -3.4f) };
            foreach (var l in legs)
            {
                var foot = new Vector3(l.x * 1.35f, 0.05f, l.z * 1.05f);
                var hip = new Vector3(l.x * 0.8f, bodyY - 0.9f, l.z);
                var knee = new Vector3(l.x * 1.3f, 1.3f, l.z * 1.03f);
                mb.For(hullDark).Beam(hip, knee, 0.26f);
                mb.For(acMat).Beam(knee, foot + Vector3.up * 0.2f, 0.16f);
                mb.For(dark).Beam(hip + Vector3.down * 0.2f + new Vector3(0, 0, 0.4f), knee + new Vector3(0, 0.3f, 0.2f), 0.1f);
                mb.For(dark).Cylinder(foot, 0.55f, 0.18f, 12, true, 0.4f);
                mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(knee, new Vector3(0.34f, 0.3f, 0.34f));
            }
            // Unterseite: Hitzeschild-Kacheln
            mb.For(dark).Box(new Vector3(0, bodyY - 1.45f, 0.2f), new Vector3(2.6f, 0.08f, 8.4f));
            mb.Build("TransportShip", Root, true);
        }
    }
}
