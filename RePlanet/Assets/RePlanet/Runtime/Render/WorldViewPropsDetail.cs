using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;
using Terrain = RePlanet.Core.Terrain;

namespace RePlanet
{
    /// <summary>
    /// Detaillierte Requisiten: Straßenlaternen (teils kaputt), Heizlaternen, Werbetafel auf Gittermasten,
    /// Bushaltestelle, Autowracks, Förderbrücken, Windräder, Schornsteine, Hafenkräne, Leuchtturm, Filterstationen,
    /// Riff, Bojen, Radar – und zusätzliches Straßenmobiliar (Ampeln, Schilder, Hydranten, Bänke, Poller).
    /// Alle Funktionen zeichnen in lokalen Koordinaten des übergebenen MultiBuilders (mb.M = Lage der Requisite).
    /// </summary>
    public partial class WorldView
    {
        /// <summary>Straßenlaterne mit Sockel, konischem Mast, geschwungenem Ausleger und Leuchtenkopf. Liefert false, wenn sie kaputt ist (leuchtet nie).</summary>
        bool StreetLamp(MultiBuilder mb, Rng rng, int area, bool heat)
        {
            var pole = Mat(heat ? new Color(0.32f, 0.36f, 0.42f) : new Color(0.24f, 0.26f, 0.27f));
            bool broken = !heat && rng.Chance(0.15f);
            var o = mb.M;
            if (broken) mb.M = o * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(rng.Range(-14f, 14f), 0, rng.Range(8f, 16f)), Vector3.one);
            mb.For(plinthMat).Cylinder(Vector3.zero, 0.24f, 0.55f, 8, true, 0.2f);
            mb.For(pole).Cylinder(new Vector3(0, 0.5f, 0), 0.12f, 4.6f, 8, true, 0.07f);
            mb.For(pole).Box(new Vector3(0.13f, 0.95f, 0), new Vector3(0.04f, 0.4f, 0.16f));
            // Ausleger in drei Segmenten
            var a = new Vector3(0, 5.0f, 0); var b = new Vector3(0, 5.35f, 0.35f); var c = new Vector3(0, 5.45f, 0.95f);
            mb.For(pole).Beam(new Vector3(0, 4.9f, 0), a, 0.09f);
            mb.For(pole).Beam(a, b, 0.08f);
            mb.For(pole).Beam(b, c, 0.08f);
            mb.For(pole).BoxRot(new Vector3(0, 5.42f, 1.25f), new Vector3(0.42f, 0.14f, 0.75f), new Vector3(-4, 0, 0));
            mb.For(broken ? glassDark : LampMat(area)).BoxRot(new Vector3(0, 5.32f, 1.27f), new Vector3(0.34f, 0.06f, 0.6f), new Vector3(-4, 0, 0));
            if (heat)
            {
                // Heizelement mit Gitter und Schneekappe
                mb.For(Mat(new Color(0.85f, 0.4f, 0.18f))).Box(new Vector3(0, 2.4f, 0.22f), new Vector3(0.36f, 0.8f, 0.22f));
                for (int k = 0; k < 5; k++) mb.For(darkMat).Box(new Vector3(0, 2.1f + k * 0.15f, 0.34f), new Vector3(0.34f, 0.03f, 0.02f));
                mb.For(Mat(new Color(0.95f, 0.97f, 1f), 0.3f)).BoxRot(new Vector3(0, 5.52f, 1.25f), new Vector3(0.44f, 0.06f, 0.7f), new Vector3(-4, 0, 0));
            }
            else if (rng.Chance(0.3f)) // Plakat/Aufkleber am Mast
                mb.For(Mat(SignCols[rng.Range(0, SignCols.Length)])).Box(new Vector3(0, 2.2f, 0.12f), new Vector3(0.22f, 0.4f, 0.02f));
            mb.M = o;
            return !broken;
        }

        void Billboard(MultiBuilder mb, Rng rng)
        {
            var steel = Mat(new Color(0.3f, 0.3f, 0.32f));
            // zwei Gittermasten
            for (int s = -1; s <= 1; s += 2)
            {
                var bx = s * 2.5f;
                for (int k = 0; k < 4; k++) mb.For(steel).Box(new Vector3(bx + (k % 2 == 0 ? -0.18f : 0.18f), 3f, k < 2 ? -0.18f : 0.18f), new Vector3(0.08f, 6f, 0.08f));
                for (float y = 0.5f; y < 5.6f; y += 1f)
                {
                    mb.For(steel).Beam(new Vector3(bx - 0.18f, y, -0.18f), new Vector3(bx + 0.18f, y + 1f, -0.18f), 0.04f);
                    mb.For(steel).Beam(new Vector3(bx - 0.18f, y, 0.18f), new Vector3(bx + 0.18f, y + 1f, 0.18f), 0.04f);
                }
            }
            // Laufsteg mit Geländer, Rahmen, Tafel mit abgerissenen Plakatbahnen, Strahler
            mb.For(steel).Box(new Vector3(0, 5.7f, -0.6f), new Vector3(8.2f, 0.08f, 0.8f));
            mb.For(steel).Box(new Vector3(0, 6.3f, -0.98f), new Vector3(8.2f, 0.05f, 0.05f));
            for (float x = -4f; x <= 4f; x += 1f) mb.For(steel).Box(new Vector3(x, 6.0f, -0.98f), new Vector3(0.04f, 0.6f, 0.04f));
            mb.For(steel).Box(new Vector3(0, 7.2f, 0.05f), new Vector3(8.4f, 3.3f, 0.2f));
            mb.For(Mat(new Color(0.92f, 0.86f, 0.7f))).Box(new Vector3(0, 7.2f, -0.08f), new Vector3(8f, 3f, 0.06f));
            for (int k = 0; k < 5; k++)
                if (rng.Chance(0.6f)) mb.For(Mat(SignCols[rng.Range(0, SignCols.Length)] * rng.Range(0.8f, 1.1f))).BoxRot(new Vector3(-3.2f + k * 1.6f, 6.3f + rng.Range(0f, 0.6f), -0.12f), new Vector3(1.5f, rng.Range(0.5f, 1.4f), 0.02f), new Vector3(0, 0, rng.Range(-8f, 8f)));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(steel).Beam(new Vector3(s * 2.5f, 5.75f, -0.9f), new Vector3(s * 2.5f, 5.95f, -1.6f), 0.05f);
                mb.For(darkMat).BoxRot(new Vector3(s * 2.5f, 6.05f, -1.7f), new Vector3(0.3f, 0.25f, 0.35f), new Vector3(-35, 0, 0));
            }
        }

        void BusStop(MultiBuilder mb, Rng rng)
        {
            var frame = Mat(new Color(0.22f, 0.3f, 0.26f));
            var yellow = Mat(new Color(0.95f, 0.75f, 0.2f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(frame).Box(new Vector3(s * 1.45f, 1.2f, -0.6f), new Vector3(0.08f, 2.4f, 0.08f));
                mb.For(frame).Box(new Vector3(s * 1.45f, 1.2f, 0.55f), new Vector3(0.08f, 2.4f, 0.08f));
                mb.For(Mats.Get(Mats.Fade, new Color(0.7f, 0.85f, 0.9f, 0.4f))).Box(new Vector3(s * 1.45f, 1.3f, 0), new Vector3(0.03f, 1.8f, 1.1f));
            }
            mb.For(Mats.Get(Mats.Fade, new Color(0.7f, 0.85f, 0.9f, 0.4f))).Box(new Vector3(-0.4f, 1.3f, -0.6f), new Vector3(2.0f, 1.8f, 0.03f));
            mb.For(glassDark).BoxRot(new Vector3(0.95f, 1.1f, -0.61f), new Vector3(0.9f, 1.4f, 0.02f), new Vector3(0, 0, 6)); // zersplitterte Scheibe
            mb.For(frame).Box(new Vector3(0, 0.25f, -0.6f), new Vector3(2.9f, 0.08f, 0.06f));
            mb.For(yellow).BoxRot(new Vector3(0, 2.5f, 0), new Vector3(3.3f, 0.12f, 1.6f), new Vector3(-4, 0, 0));
            // Bank
            mb.For(woodMat).Box(new Vector3(0, 0.48f, -0.35f), new Vector3(2.2f, 0.06f, 0.4f));
            mb.For(frame).Box(new Vector3(-0.9f, 0.24f, -0.35f), new Vector3(0.06f, 0.48f, 0.35f));
            mb.For(frame).Box(new Vector3(0.9f, 0.24f, -0.35f), new Vector3(0.06f, 0.48f, 0.35f));
            // Haltestellenschild mit Fahrplan, überquellender Mülleimer
            mb.For(frame).Cylinder(new Vector3(1.9f, 0, 0.4f), 0.05f, 2.9f, 6);
            mb.For(yellow).CylinderZ(new Vector3(1.9f, 2.75f, 0.4f), 0.3f, 0.04f, 14);
            mb.For(Mat(new Color(0.2f, 0.5f, 0.3f))).CylinderZ(new Vector3(1.9f, 2.75f, 0.43f), 0.2f, 0.03f, 14);
            mb.For(signLight).Box(new Vector3(1.9f, 1.6f, 0.46f), new Vector3(0.35f, 0.5f, 0.04f));
            mb.For(frame).Cylinder(new Vector3(-1.9f, 0, 0.3f), 0.25f, 0.8f, 10, false);
            mb.For(Mat(new Color(0.16f, 0.17f, 0.18f))).Crumple(new Vector3(-1.9f, 0.85f, 0.3f), 0.28f, 0.6f, 4, 0.25f, 7, 4);
            mb.For(signLight).Crumple(new Vector3(-1.55f, 0.1f, 0.55f), 0.16f, 0.6f, 5, 0.3f, 6, 3);
        }

        /// <summary>Autowrack (Limousine oder Kastenwagen) mit Scheiben, Leuchten, Stoßstangen, Rädern/Ziegelsteinen, offener Tür oder Haube, Rost.</summary>
        void CarWreck(MultiBuilder mb, Rng rng, int style)
        {
            var body = Mat(new[] { new Color(0.55f, 0.3f, 0.22f), new Color(0.35f, 0.4f, 0.45f), new Color(0.6f, 0.52f, 0.3f), new Color(0.3f, 0.36f, 0.3f), new Color(0.62f, 0.62f, 0.6f), new Color(0.5f, 0.2f, 0.18f) }[style % 6]);
            var rust = Mat(new Color(0.42f, 0.24f, 0.15f));
            var tire = Mat(new Color(0.1f, 0.1f, 0.11f));
            bool van = style % 3 == 2;
            if (van)
            {
                mb.For(body).Box(new Vector3(0, 1.15f, -0.3f), new Vector3(1.9f, 1.7f, 3.6f));
                mb.For(body).BoxRot(new Vector3(0, 0.95f, 1.75f), new Vector3(1.88f, 1.2f, 0.9f), new Vector3(8, 0, 0));
                mb.For(glassDark).BoxRot(new Vector3(0, 1.55f, 1.62f), new Vector3(1.7f, 0.6f, 0.05f), new Vector3(-25, 0, 0));
                mb.For(glassDark).Box(new Vector3(0.96f, 1.55f, 1.1f), new Vector3(0.03f, 0.5f, 0.8f));
                mb.For(glassDark).Box(new Vector3(-0.96f, 1.55f, 1.1f), new Vector3(0.03f, 0.5f, 0.8f));
                mb.For(signLight).Box(new Vector3(0.96f, 1.3f, -0.6f), new Vector3(0.02f, 0.35f, 2.2f));
            }
            else
            {
                mb.For(body).Box(new Vector3(0, 0.6f, 0.05f), new Vector3(1.8f, 0.6f, 4.0f));
                mb.For(body).BoxJ(new Vector3(0, 1.13f, -0.3f), new Vector3(1.56f, 0.5f, 1.9f), Vector3.zero, 0.05f, style);
                mb.For(glassDark).BoxRot(new Vector3(0, 1.15f, 0.72f), new Vector3(1.45f, 0.5f, 0.04f), new Vector3(-32, 0, 0));
                mb.For(glassDark).BoxRot(new Vector3(0, 1.15f, -1.3f), new Vector3(1.4f, 0.45f, 0.04f), new Vector3(30, 0, 0));
                mb.For(glassDark).Box(new Vector3(0.79f, 1.15f, -0.3f), new Vector3(0.02f, 0.36f, 1.6f));
                mb.For(glassDark).Box(new Vector3(-0.79f, 1.15f, -0.3f), new Vector3(0.02f, 0.36f, 1.6f));
                if (rng.Chance(0.4f)) mb.For(body).BoxRot(new Vector3(0, 1.25f, 1.55f), new Vector3(1.7f, 0.06f, 1.1f), new Vector3(-55, 0, 0)); // offene Haube
                else mb.For(body).Box(new Vector3(0, 0.93f, 1.45f), new Vector3(1.7f, 0.06f, 1.1f));
                if (rng.Chance(0.3f)) mb.For(body).BoxRot(new Vector3(1.35f, 0.75f, 0.35f), new Vector3(0.05f, 0.7f, 1.1f), new Vector3(0, 55, 0)); // offene Tür
            }
            float front = van ? 2.2f : 2.05f, back = van ? -2.1f : -1.95f;
            mb.For(acMat).Box(new Vector3(0, 0.42f, front), new Vector3(1.86f, 0.18f, 0.12f));
            mb.For(acMat).Box(new Vector3(0, 0.42f, back), new Vector3(1.86f, 0.18f, 0.12f));
            mb.For(signLight).Box(new Vector3(0.62f, 0.72f, front - 0.03f), new Vector3(0.3f, 0.14f, 0.05f));
            mb.For(signLight).Box(new Vector3(-0.62f, 0.72f, front - 0.03f), new Vector3(0.3f, 0.14f, 0.05f));
            mb.For(Mat(new Color(0.75f, 0.15f, 0.12f))).Box(new Vector3(0.65f, 0.76f, back + 0.03f), new Vector3(0.28f, 0.14f, 0.05f));
            mb.For(Mat(new Color(0.75f, 0.15f, 0.12f))).Box(new Vector3(-0.65f, 0.76f, back + 0.03f), new Vector3(0.28f, 0.14f, 0.05f));
            // Rostflecken
            for (int k = 0; k < 3; k++) mb.For(rust).BoxJ(new Vector3(rng.Range(-0.6f, 0.6f), van ? 2.01f : 0.91f, rng.Range(-1.4f, 1.4f)), new Vector3(rng.Range(0.3f, 0.7f), 0.02f, rng.Range(0.3f, 0.8f)), Vector3.zero, 0.05f, k);
            // Räder (manche fehlen: Wagen liegt auf Ziegeln)
            for (int i = 0; i < 4; i++)
            {
                var wp = new Vector3(i % 2 == 0 ? 0.86f : -0.86f, 0.34f, i < 2 ? 1.3f : -1.25f);
                if (rng.Chance(0.2f)) { mb.For(Mat(new Color(0.6f, 0.3f, 0.22f))).Box(wp + Vector3.down * 0.18f, new Vector3(0.3f, 0.3f, 0.45f)); continue; }
                mb.For(tire).CylinderX(wp, 0.34f, 0.24f, 10);
                mb.For(acMat).CylinderX(wp + new Vector3(Mathf.Sign(wp.x) * 0.02f, 0, 0), 0.19f, 0.25f, 8);
            }
        }

        /// <summary>Förderbrücke über der Straße (PYRA): Gitterstützen, Fachwerkträger, Band mit Rollen und Schrottstücken.</summary>
        void Gantry(MultiBuilder mb, Rng rng)
        {
            var rust = Mat(new Color(0.5f, 0.3f, 0.2f));
            var dark = Mat(new Color(0.2f, 0.2f, 0.22f));
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 7f;
                for (int k = 0; k < 4; k++) mb.For(rust).Box(new Vector3(x + (k % 2 == 0 ? -0.35f : 0.35f), 4.1f, k < 2 ? -0.5f : 0.5f), new Vector3(0.14f, 8.2f, 0.14f));
                for (float y = 0.5f; y < 7.5f; y += 1.4f)
                {
                    mb.For(rust).Beam(new Vector3(x - 0.35f, y, -0.5f), new Vector3(x + 0.35f, y + 1.4f, -0.5f), 0.06f);
                    mb.For(rust).Beam(new Vector3(x - 0.35f, y, 0.5f), new Vector3(x + 0.35f, y + 1.4f, 0.5f), 0.06f);
                }
                mb.For(plinthMat).Box(new Vector3(x, 0.2f, 0), new Vector3(1.2f, 0.4f, 1.5f));
            }
            // Fachwerkträger
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(rust).Box(new Vector3(0, 8.2f, s * 0.75f), new Vector3(16f, 0.14f, 0.14f));
                mb.For(rust).Box(new Vector3(0, 9.4f, s * 0.75f), new Vector3(16f, 0.14f, 0.14f));
                for (float x = -7.5f; x < 7.5f; x += 1.5f) mb.For(rust).Beam(new Vector3(x, 8.2f, s * 0.75f), new Vector3(x + 1.5f, 9.4f, s * 0.75f), 0.07f);
            }
            mb.For(dark).Box(new Vector3(0, 8.5f, 0), new Vector3(16f, 0.08f, 1.2f));
            for (float x = -7.5f; x < 7.6f; x += 0.8f) mb.For(acMat).CylinderZ(new Vector3(x, 8.4f, 0), 0.06f, 1.3f, 6);
            for (int k = 0; k < 5; k++) mb.For(Mat(new Color(0.45f + rng.Range(0f, 0.2f), 0.35f, 0.28f))).BoxJ(new Vector3(rng.Range(-6f, 6f), 8.72f, rng.Range(-0.3f, 0.3f)), new Vector3(0.6f, 0.4f, 0.5f), new Vector3(0, rng.Range(0f, 90f), 0), 0.08f, k);
            mb.For(Mat(new Color(0.95f, 0.75f, 0.15f))).Box(new Vector3(0, 7.95f, 0.83f), new Vector3(16f, 0.35f, 0.04f));
        }

        void TurbineTower(MultiBuilder mb)
        {
            var white = Mat(new Color(0.88f, 0.87f, 0.84f));
            mb.For(plinthMat).Cylinder(Vector3.zero, 1.1f, 0.6f, 12);
            mb.For(white).Cylinder(new Vector3(0, 0.6f, 0), 0.7f, 21.4f, 12, true, 0.38f);
            mb.For(Mat(new Color(0.85f, 0.3f, 0.22f))).Cylinder(new Vector3(0, 3f, 0), 0.66f, 0.6f, 12, false, 0.645f);
            mb.For(white).Box(new Vector3(0, 22.1f, -0.4f), new Vector3(1.1f, 1.1f, 2.6f));
            mb.For(darkMat).Box(new Vector3(0, 22.7f, -1.2f), new Vector3(0.6f, 0.1f, 0.6f));
        }

        void Chimney(MultiBuilder mb)
        {
            var brick = Mat(new Color(0.45f, 0.3f, 0.25f));
            mb.For(plinthMat).Box(new Vector3(0, 1.2f, 0), new Vector3(3.4f, 2.4f, 3.4f));
            mb.For(brick).Cylinder(Vector3.zero, 1.3f, 26f, 14, true, 1f);
            for (float y = 5f; y < 25f; y += 5f) mb.For(darkMat).Cylinder(new Vector3(0, y, 0), Mathf.Lerp(1.32f, 1.02f, y / 26f), 0.35f, 14);
            mb.For(Mat(new Color(0.85f, 0.85f, 0.8f))).Cylinder(new Vector3(0, 21f, 0), 1.08f, 1.2f, 14);
            mb.For(darkMat).Cylinder(new Vector3(0, 25.6f, 0), 1.1f, 0.5f, 14);
            for (float y = 2.6f; y < 25f; y += 0.45f) mb.For(railMat).Box(new Vector3(0, y, 1.35f - y * 0.011f), new Vector3(0.45f, 0.04f, 0.04f));
        }

        /// <summary>Portal-Hafenkran: vier Beine mit Kreuzstreben, Querträger, Ausleger über dem Wasser, Kabine, Maschinenhaus, Seile, Spreader.</summary>
        void HarborCrane(MultiBuilder mb, Rng rng)
        {
            var y = Mat(new Color(0.9f, 0.6f, 0.15f));
            var red = Mat(new Color(0.8f, 0.25f, 0.18f));
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -3f : 3f, 0, i < 2 ? -2f : 2f);
                mb.For(y).Box(p + Vector3.up * 7f, new Vector3(0.7f, 14f, 0.7f));
                mb.For(darkMat).Box(p + Vector3.up * 0.3f, new Vector3(1f, 0.6f, 1.4f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(y).Beam(new Vector3(s * 3f, 1.5f, -2f), new Vector3(s * 3f, 12f, 2f), 0.25f);
                mb.For(y).Beam(new Vector3(s * 3f, 1.5f, 2f), new Vector3(s * 3f, 12f, -2f), 0.25f);
                mb.For(y).Box(new Vector3(s * 3f, 7f, 0), new Vector3(0.5f, 0.5f, 4f));
            }
            mb.For(y).Box(new Vector3(0, 13.4f, -2f), new Vector3(7f, 1f, 0.8f));
            mb.For(y).Box(new Vector3(0, 13.4f, 2f), new Vector3(7f, 1f, 0.8f));
            // Ausleger (Fachwerk) nach vorn über das Wasser und kurz nach hinten
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(y).Box(new Vector3(s * 0.8f, 14.4f, 4f), new Vector3(0.3f, 0.3f, 24f));
                mb.For(y).Box(new Vector3(s * 0.8f, 15.8f, 1f), new Vector3(0.25f, 0.25f, 14f));
                for (float z = -7.5f; z < 15.5f; z += 2f) mb.For(y).Beam(new Vector3(s * 0.8f, 14.4f, z), new Vector3(s * 0.8f, z < 8f ? 15.8f : 14.4f + (16f - z) * 0.1f, z + 1f), 0.1f);
            }
            for (float z = -7.5f; z < 16f; z += 2f) mb.For(y).Box(new Vector3(0, 14.4f, z), new Vector3(1.6f, 0.12f, 0.12f));
            mb.For(y).Beam(new Vector3(0, 19f, 0), new Vector3(0, 14.6f, 15.5f), 0.12f);
            mb.For(y).Beam(new Vector3(0, 19f, 0), new Vector3(0, 14.6f, -7.5f), 0.12f);
            mb.For(y).Box(new Vector3(0, 17f, 0), new Vector3(0.5f, 4.5f, 0.5f));
            // Maschinenhaus, Kabine, Warnlicht
            mb.For(red).Box(new Vector3(0, 15.4f, -5.5f), new Vector3(3f, 2f, 3.5f));
            mb.For(y).Box(new Vector3(1.2f, 13.2f, 3.5f), new Vector3(1.5f, 1.6f, 1.6f));
            mb.For(glassDark).Box(new Vector3(1.2f, 13.3f, 4.32f), new Vector3(1.3f, 1f, 0.04f));
            mb.For(Glow(new Color(1f, 0.25f, 0.1f), 3f)).Sphere(new Vector3(0, 19.3f, 0), 0.25f, 6, 4);
            // Laufkatze mit Seilen und Spreader
            float tz = rng.Range(6f, 13f), hang = rng.Range(4f, 10f);
            mb.For(darkMat).Box(new Vector3(0, 14.1f, tz), new Vector3(1.8f, 0.5f, 1.2f));
            mb.For(darkMat).Beam(new Vector3(-0.4f, 13.9f, tz), new Vector3(-0.4f, 13.9f - hang, tz), 0.04f);
            mb.For(darkMat).Beam(new Vector3(0.4f, 13.9f, tz), new Vector3(0.4f, 13.9f - hang, tz), 0.04f);
            mb.For(y).Box(new Vector3(0, 13.7f - hang, tz), new Vector3(2.4f, 0.35f, 0.8f));
        }

        void Lighthouse(MultiBuilder mb, int area)
        {
            var white = Mat(new Color(0.95f, 0.95f, 0.92f));
            var red = Mat(new Color(0.85f, 0.25f, 0.2f));
            var rock = Mat(new Color(0.5f, 0.48f, 0.44f));
            for (int k = 0; k < 6; k++) mb.For(rock).Blob(new Vector3(Mathf.Cos(k) * 2.6f, -0.4f, Mathf.Sin(k) * 2.6f), 1.2f + (k % 3) * 0.3f, 0.9f, 7, 2, k, 0.35f);
            mb.For(plinthMat).Cylinder(Vector3.zero, 2.6f, 0.8f, 16);
            mb.For(white).Cylinder(new Vector3(0, 0.8f, 0), 2.2f, 17.2f, 16, true, 1.6f);
            for (int k = 0; k < 3; k++) mb.For(red).Cylinder(new Vector3(0, 4f + k * 4.5f, 0), Mathf.Lerp(2.08f, 1.68f, (4f + k * 4.5f) / 18f) + 0.02f, 2.2f, 16, false, Mathf.Lerp(2.08f, 1.68f, (6.2f + k * 4.5f) / 18f) + 0.02f);
            mb.For(darkMat).Box(new Vector3(0, 1.9f, 2.12f), new Vector3(0.9f, 2f, 0.1f));
            for (int k = 0; k < 3; k++) mb.For(glassDark).Box(new Vector3(0, 6.5f + k * 4.5f, Mathf.Lerp(2.05f, 1.65f, (6.5f + k * 4.5f) / 18f)), new Vector3(0.5f, 0.8f, 0.1f));
            // Galerie mit Geländer, Laternenraum, Kuppel
            mb.For(darkMat).Cylinder(new Vector3(0, 18f, 0), 2.3f, 0.25f, 16);
            mb.For(railMat).Torus(new Vector3(0, 19f, 0), 2.2f, 0.05f, 20, 3);
            for (int k = 0; k < 12; k++) { float a = k / 12f * 6.283f; mb.For(railMat).Box(new Vector3(Mathf.Cos(a) * 2.2f, 18.6f, Mathf.Sin(a) * 2.2f), new Vector3(0.05f, 0.8f, 0.05f)); }
            mb.For(LampMat(area)).Cylinder(new Vector3(0, 18.25f, 0), 1.25f, 2.2f, 12);
            for (int k = 0; k < 6; k++) { float a = k / 6f * 6.283f; mb.For(darkMat).Box(new Vector3(Mathf.Cos(a) * 1.27f, 19.35f, Mathf.Sin(a) * 1.27f), new Vector3(0.08f, 2.2f, 0.08f)); }
            mb.For(red).Cylinder(new Vector3(0, 20.45f, 0), 1.45f, 1.2f, 12, true, 0.2f);
            mb.For(darkMat).Cylinder(new Vector3(0, 21.6f, 0), 0.06f, 1.2f, 5);
        }

        /// <summary>Filterstation: aus = verrosteter Tank mit Leck; an = sauberer Tank mit leuchtenden Filterfenstern.</summary>
        void FilterStation(MultiBuilder off, MultiBuilder on)
        {
            var rust = Mat(new Color(0.5f, 0.3f, 0.2f));
            var rustD = Mat(new Color(0.35f, 0.2f, 0.14f));
            off.For(rust).Cylinder(new Vector3(0, 0, 0), 1.9f, 3.2f, 16);
            off.For(rustD).Cylinder(new Vector3(0, 3.2f, 0), 1.95f, 0.4f, 16, true, 1.4f);
            off.For(rustD).Tube(new Vector3(1.6f, 1.2f, 0), new Vector3(3.2f, 0.2f, 0.6f), 0.25f, 8);
            off.For(rustD).BoxJ(new Vector3(-1.2f, 1.5f, 1.4f), new Vector3(0.8f, 0.9f, 0.05f), new Vector3(0, 40, 0), 0.1f, 3);
            off.For(Mat(new Color(0.1f, 0.09f, 0.08f))).Blob(new Vector3(2.8f, 0f, 0.8f), 1.5f, 0.05f, 10, 2, 5, 0.3f);
            var white = Mat(new Color(0.85f, 0.9f, 0.92f));
            var glow = Glow(new Color(0.2f, 0.8f, 0.9f), 1.8f);
            on.For(white).Cylinder(new Vector3(0, 0, 0), 1.9f, 3.2f, 16);
            on.For(Mat(new Color(0.18f, 0.72f, 0.68f))).Cylinder(new Vector3(0, 3.2f, 0), 1.95f, 0.4f, 16, true, 1.4f);
            for (int k = 0; k < 4; k++) { float a = k * 1.571f + 0.4f; on.For(glow).BoxRot(new Vector3(Mathf.Cos(a) * 1.9f, 1.8f, Mathf.Sin(a) * 1.9f), new Vector3(0.7f, 1.2f, 0.08f), new Vector3(0, 90 - a * Mathf.Rad2Deg, 0)); }
            on.For(glow).Cylinder(new Vector3(0, 3.6f, 0), 0.9f, 0.6f, 12);
            on.For(acMat).Tube(new Vector3(1.6f, 1.2f, 0), new Vector3(3.4f, 0.4f, 0.4f), 0.25f, 8);
            on.For(acMat).Cylinder(new Vector3(3.4f, 0f, 0.4f), 0.35f, 0.6f, 8);
        }

        /// <summary>Riff (nach Projekt): Geweihkorallen, Fächer, Hirnkorallen, leuchtende Anemonen, Seegras.</summary>
        void Reef(MultiBuilder on, Rng rng)
        {
            var colors = new[] { new Color(1f, 0.45f, 0.5f), new Color(1f, 0.7f, 0.3f), new Color(0.6f, 0.4f, 1f), new Color(0.3f, 0.9f, 0.7f), new Color(0.95f, 0.35f, 0.7f) };
            for (int i = 0; i < 60; i++)
            {
                float a = rng.Range(0, 6.28f), r = Mathf.Sqrt(rng.Next()) * 14f;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                var m = Mat(colors[i % colors.Length]);
                switch (i % 5)
                {
                    case 0: // Geweihkoralle
                        {
                            var top = p + new Vector3(rng.Range(-0.3f, 0.3f), rng.Range(0.9f, 1.8f), rng.Range(-0.3f, 0.3f));
                            on.For(m).Tube(p, top, 0.12f, 5, false, 0.07f);
                            for (int k = 0; k < 3; k++) { var q = Vector3.Lerp(p, top, 0.4f + k * 0.2f); on.For(m).Tube(q, q + new Vector3(rng.Range(-0.6f, 0.6f), rng.Range(0.3f, 0.7f), rng.Range(-0.6f, 0.6f)), 0.07f, 5, true, 0.03f); }
                            break;
                        }
                    case 1: // Fächerkoralle
                        on.For(m).BoxJ(p + Vector3.up * 0.7f, new Vector3(1.4f, 1.2f, 0.05f), new Vector3(rng.Range(-10f, 10f), rng.Range(0f, 180f), 0), 0.15f, i);
                        break;
                    case 2: // Hirnkoralle
                        on.For(m).Crumple(p, rng.Range(0.4f, 0.8f), 0.6f, i, 0.2f, 9, 5);
                        break;
                    case 3: // Anemone mit leuchtenden Spitzen
                        on.For(m).Cylinder(p, 0.2f, 0.4f, 8, false, 0.3f);
                        for (int k = 0; k < 6; k++) { float b = k * 1.047f; on.For(Glow(colors[(i + 2) % colors.Length], 1.5f)).Tube(p + Vector3.up * 0.4f, p + new Vector3(Mathf.Cos(b) * 0.35f, 0.75f, Mathf.Sin(b) * 0.35f), 0.03f, 4, true, 0.015f); }
                        break;
                    default: // Seegras
                        for (int k = 0; k < 4; k++) on.For(Mat(new Color(0.3f, 0.6f, 0.35f))).Tube(p + new Vector3(k * 0.1f, 0, 0), p + new Vector3(k * 0.1f + rng.Range(-0.3f, 0.3f), rng.Range(1f, 2.2f), rng.Range(-0.3f, 0.3f)), 0.04f, 4, false, 0.01f);
                        break;
                }
            }
        }

        void NavBuoy(MultiBuilder mb, int style)
        {
            var c = style == 0 ? new Color(0.9f, 0.3f, 0.2f) : style == 1 ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.2f, 0.65f, 0.35f);
            mb.For(Mat(c)).Cylinder(new Vector3(0, -0.5f, 0), 0.75f, 0.9f, 12, true, 0.6f);
            mb.For(signLight).Cylinder(new Vector3(0, 0.1f, 0), 0.66f, 0.18f, 12, false, 0.62f);
            for (int k = 0; k < 3; k++) { float a = k * 2.094f; mb.For(Mat(c)).Beam(new Vector3(Mathf.Cos(a) * 0.45f, 0.35f, Mathf.Sin(a) * 0.45f), new Vector3(0, 1.9f, 0), 0.06f); }
            mb.For(Mat(c)).Cylinder(new Vector3(0, 1.3f, 0), 0.3f, 0.3f, 8, false);
            mb.For(Glow(c, 2f)).Sphere(new Vector3(0, 2.0f, 0), 0.12f, 6, 4);
        }

        void RadarBase(MultiBuilder mb)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.094f;
                mb.For(railMat).Beam(new Vector3(Mathf.Cos(a) * 1.4f, 0, Mathf.Sin(a) * 1.4f), new Vector3(0, 5.2f, 0), 0.12f);
            }
            mb.For(Mat(new Color(0.85f, 0.88f, 0.92f))).Box(new Vector3(0, 5.5f, 0), new Vector3(1.4f, 1f, 1.4f));
            mb.For(WindowMat(0)).Box(new Vector3(0, 5.6f, 0.71f), new Vector3(0.9f, 0.4f, 0.04f));
            mb.For(darkMat).Cylinder(new Vector3(0, 6f, 0), 0.3f, 0.5f, 8);
            mb.For(Mat(new Color(0.95f, 0.97f, 1f), 0.3f)).Box(new Vector3(0, 6.02f, 0), new Vector3(1.5f, 0.08f, 1.5f));
        }

        // ------------------------------------------------------------------ Straßenmobiliar
        /// <summary>Ampeln an Kreuzungen, Verkehrsschilder, Hydranten, Poller, Parkbänke – nur auf freien Gehwegflächen.</summary>
        void BuildStreetFurniture(ChunkBuilder cb)
        {
            if (Planet != "terra" && Planet != "nivalis") return;
            var rng = new Rng(Def.Seed + 404);
            var tmp = new List<Box>();
            System.Func<float, float, bool> free = (x, z) =>
            {
                if (Mathf.Abs(x) > 146f || Mathf.Abs(z) > 146f || Layout.Base.InBase(x, z) || (Mathf.Abs(x) < 40f && z < -88f)) return false;
                if (Mathf.Abs(z + 50f) < 8f || Mathf.Abs(z - 50f) < 8f) return false;
                Layout.Query(x, z, 1.5f, tmp);
                foreach (var b in tmp) if (b.Solid && b.Gate < 0 && b.Contains(x, z, 0.6f)) return false;
                foreach (var t in Layout.Trash) if ((t.Pos.x - x) * (t.Pos.x - x) + (t.Pos.z - z) * (t.Pos.z - z) < 2.2f) return false;
                foreach (var p in Layout.ProjectSites) if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < 144f) return false;
                return true;
            };
            var dark = Mat(new Color(0.2f, 0.22f, 0.22f));
            var xs = new List<float> { 0f };
            foreach (var x in Terrain.RoadX) xs.Add(x);
            foreach (var rz in Terrain.RoadZ)
                foreach (var rx in xs)
                {
                    if (rz > 145f) continue;
                    float hx = rx == 0 ? 8f : 5f, hz = 6f;
                    for (int k = 0; k < 2; k++)
                    {
                        float sx = k == 0 ? 1 : -1, sz = k == 0 ? 1 : -1;
                        float x = rx + sx * (hx + 0.9f), z = rz + sz * (hz + 0.9f);
                        if (!free(x, z)) continue;
                        float yaw = k == 0 ? 270f : 90f; // Ausleger zeigt über die Fahrbahn
                        var mb = cb.At(x, z);
                        mb.M = Matrix4x4.TRS(new Vector3(x, Terrain.HeightAt(Planet, x, z), z), Quaternion.Euler(0, yaw + (rng.Chance(0.2f) ? rng.Range(-15f, 15f) : 0f), rng.Chance(0.15f) ? rng.Range(5f, 12f) : 0f), Vector3.one);
                        TrafficLight(mb, rx == 0 ? hx + 0.9f : 0f);
                    }
                }
            // Schilder, Hydranten, Poller, Bänke entlang der Gehwege
            for (int i = 0; i < 90; i++)
            {
                bool alongMain = rng.Chance(0.5f);
                float x, z, yaw;
                if (alongMain) { float side = rng.Chance(0.5f) ? 1 : -1; x = side * 9.2f; z = rng.Range(-86f, 144f); yaw = side > 0 ? 270f : 90f; }
                else { var rz = Terrain.RoadZ[rng.Range(0, Terrain.RoadZ.Length)]; float side = rng.Chance(0.5f) ? 1 : -1; x = rng.Range(-140f, 140f); z = rz + side * 6.9f; yaw = side > 0 ? 180f : 0f; }
                if (!free(x, z)) continue;
                var mb = cb.At(x, z);
                mb.M = Matrix4x4.TRS(new Vector3(x, Terrain.HeightAt(Planet, x, z), z), Quaternion.Euler(0, yaw, 0), Vector3.one);
                switch (i % 4)
                {
                    case 0: // Verkehrsschild (rund oder dreieckig), manchmal verbogen
                        {
                            var o = mb.M;
                            if (rng.Chance(0.3f)) mb.M = o * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(rng.Range(-20f, 20f), 0, rng.Range(-15f, 15f)), Vector3.one);
                            mb.For(railMat).Cylinder(Vector3.zero, 0.04f, 2.5f, 6);
                            if (rng.Chance(0.5f)) { mb.For(Mat(new Color(0.8f, 0.15f, 0.12f))).CylinderZ(new Vector3(0, 2.4f, 0.05f), 0.32f, 0.03f, 14); mb.For(signLight).CylinderZ(new Vector3(0, 2.4f, 0.07f), 0.24f, 0.02f, 14); }
                            else { mb.For(Mat(new Color(0.2f, 0.4f, 0.75f))).Box(new Vector3(0, 2.3f, 0.05f), new Vector3(0.55f, 0.55f, 0.03f)); mb.For(signLight).Box(new Vector3(0, 2.3f, 0.07f), new Vector3(0.12f, 0.35f, 0.01f)); }
                            mb.M = o;
                            break;
                        }
                    case 1: // Hydrant
                        mb.For(Mat(new Color(0.8f, 0.18f, 0.14f))).Cylinder(Vector3.zero, 0.14f, 0.6f, 8);
                        mb.For(Mat(new Color(0.8f, 0.18f, 0.14f))).Cylinder(new Vector3(0, 0.6f, 0), 0.16f, 0.14f, 8, true, 0.05f);
                        mb.For(acMat).CylinderX(new Vector3(0, 0.42f, 0), 0.06f, 0.42f, 6);
                        break;
                    case 2: // Poller
                        for (int k = -1; k <= 1; k++) mb.For(k == 0 ? Mat(new Color(0.95f, 0.75f, 0.2f)) : dark).Cylinder(new Vector3(k * 1.2f, 0, 0), 0.1f, 0.85f, 8, true, 0.08f);
                        break;
                    default: // Parkbank mit Armlehnen
                        mb.For(woodMat).Box(new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.06f, 0.45f));
                        mb.For(woodMat).BoxRot(new Vector3(0, 0.8f, -0.22f), new Vector3(1.8f, 0.35f, 0.05f), new Vector3(-10, 0, 0));
                        mb.For(dark).Box(new Vector3(-0.8f, 0.3f, 0), new Vector3(0.07f, 0.6f, 0.5f));
                        mb.For(dark).Box(new Vector3(0.8f, 0.3f, 0), new Vector3(0.07f, 0.6f, 0.5f));
                        break;
                }
            }
        }

        void TrafficLight(MultiBuilder mb, float arm)
        {
            var pole = Mat(new Color(0.24f, 0.26f, 0.27f));
            var housing = Mat(new Color(0.13f, 0.14f, 0.15f));
            mb.For(pole).Cylinder(Vector3.zero, 0.09f, arm > 0 ? 5.6f : 3.2f, 8);
            var off = new[] { Mat(new Color(0.35f, 0.08f, 0.06f), 0.8f), Mat(new Color(0.4f, 0.3f, 0.06f), 0.8f), Mat(new Color(0.06f, 0.3f, 0.12f), 0.8f) };
            System.Action<Vector3> head = p =>
            {
                mb.For(housing).Box(p, new Vector3(0.36f, 1.0f, 0.28f));
                for (int k = 0; k < 3; k++)
                {
                    mb.For(off[k]).CylinderZ(p + new Vector3(0, 0.3f - k * 0.3f, 0.15f), 0.1f, 0.03f, 10);
                    mb.For(housing).Box(p + new Vector3(0, 0.4f - k * 0.3f, 0.2f), new Vector3(0.26f, 0.03f, 0.12f));
                }
            };
            if (arm > 0)
            {
                mb.For(pole).Beam(new Vector3(0, 5.5f, 0), new Vector3(0, 5.5f, arm), 0.1f);
                head(new Vector3(0, 5.0f, arm * 0.7f));
            }
            head(new Vector3(0, 2.6f, 0.2f));
            mb.For(Mat(new Color(0.85f, 0.85f, 0.8f))).Box(new Vector3(0, 1.1f, 0.12f), new Vector3(0.14f, 0.2f, 0.1f)); // Taster
        }
    }
}
