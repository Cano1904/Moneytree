using System.Collections.Generic;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Containerfrachter (lokal: +Z = Nase, Ursprung = Bodenpunkt bei ausgefahrenen Beinen, ≈ 38 m lang):
    /// Rückgrat mit Aufbau, Brücke mit Fensterband, abgeflachte Nase mit Kanzel, drei Haupttriebwerke mit Glocken,
    /// vier Hubtriebwerke an Auslegern, Kühlrippen, Antennenmast, Radarschüssel, Positions- und Blinklichter, Scheinwerfergehäuse,
    /// ein Gestell mit 2 × 4 farbigen Wellblech-Containern (Rippen, Eckpfosten, Innenverkleidung, Innenlicht),
    /// je Container zwei Bodenklappen mit Warnstreifen, vier Teleskop-Landebeine mit Tellern.
    /// Nur Geometrie (keine Lichter/Partikel) – Animation und Effekte: <see cref="ShipArrival"/>.
    /// </summary>
    public sealed class FreighterModel
    {
        public const float SpineY = 8.9f, RackBottom = 3.5f, RackTop = 7.6f;
        public static readonly float[] BellX = { -2.5f, 0f, 2.5f };
        public static readonly Vector3[] LiftPos = { new Vector3(-7.4f, 8.2f, -9.5f), new Vector3(7.4f, 8.2f, -9.5f), new Vector3(-7.4f, 8.2f, 7f), new Vector3(7.4f, 8.2f, 7f) };

        public Transform Root;
        /// <summary>Bodenklappen (Drehpunkt = Scharnier an der Außenkante) und ihre Seite (−1/+1).</summary>
        public readonly List<Transform> Flaps = new List<Transform>();
        public readonly List<float> FlapSign = new List<float>();
        /// <summary>Landebeine und ihre Lage bei ausgefahrenem Bein.</summary>
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<Vector3> LegBase = new List<Vector3>();
        /// <summary>Unterkanten-Mitte je Container (lokal) – dort fallen die Teile heraus.</summary>
        public readonly List<Vector3> ContainerBottoms = new List<Vector3>();
        public Material NozzleMat, FloodMat, BeaconRed, BeaconGreen, BeaconWhite;

        public static FreighterModel Build(Transform parent)
        {
            var m = new FreighterModel();
            var root = new GameObject("Containerfrachter").transform;
            root.SetParent(parent, false);
            m.Root = root;
            var hull = Mats.Get(Mats.Metal, new Color(0.8f, 0.82f, 0.85f));
            var hullDark = Mats.Get(Mats.Metal, new Color(0.36f, 0.38f, 0.42f));
            var dark = Mats.Get(Mats.Opaque, new Color(0.13f, 0.14f, 0.16f));
            var inner = Mats.Get(Mats.Opaque, new Color(0.2f, 0.19f, 0.18f));
            var accent = Mats.Get(Mats.Opaque, new Color(1f, 0.55f, 0.18f));
            var teal = Mats.Get(Mats.Opaque, new Color(0.18f, 0.7f, 0.66f));
            var yellow = Mats.Get(Mats.Opaque, new Color(0.95f, 0.78f, 0.18f));
            var glass = Mats.Get(Mats.Emissive, new Color(0.1f, 0.2f, 0.28f), new Color(0.15f, 0.5f, 0.7f));
            var winGlow = Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.55f), new Color(2.2f, 1.7f, 1f));
            m.NozzleMat = Mats.Unique(Mats.Emissive, new Color(0.6f, 0.85f, 1f));
            m.FloodMat = Mats.Unique(Mats.Emissive, new Color(1f, 0.97f, 0.9f));
            m.BeaconRed = Mats.Unique(Mats.Emissive, new Color(1f, 0.2f, 0.15f));
            m.BeaconGreen = Mats.Unique(Mats.Emissive, new Color(0.2f, 1f, 0.35f));
            m.BeaconWhite = Mats.Unique(Mats.Emissive, Color.white);

            var mb = new MultiBuilder { UsePalette = true };
            // Rückgrat und Aufbau
            mb.For(hull).Box(new Vector3(0f, SpineY, -1.5f), new Vector3(4.4f, 2.6f, 30f));
            mb.For(hullDark).Box(new Vector3(0f, SpineY + 1.6f, -3.5f), new Vector3(3.2f, 0.7f, 22f));
            mb.For(dark).Box(new Vector3(0f, SpineY - 1.35f, -1.5f), new Vector3(4.6f, 0.1f, 29f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(accent).Box(new Vector3(s * 2.22f, SpineY + 0.45f, -1.5f), new Vector3(0.06f, 0.42f, 28f));
                mb.For(teal).Box(new Vector3(s * 2.23f, SpineY - 0.2f, -1.5f), new Vector3(0.05f, 0.14f, 28f));
                for (int k = 0; k < 7; k++) mb.For(hullDark).Box(new Vector3(s * 2.22f, SpineY, -14f + k * 4.2f), new Vector3(0.08f, 2.5f, 0.08f)); // Plattenfugen
            }
            // Nase mit Kanzel
            mb.M = Matrix4x4.TRS(new Vector3(0f, SpineY, 13.5f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, 0.62f));
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(2.25f, 0.02f), new Vector2(2.35f, 1.4f), new Vector2(2.05f, 3.2f), new Vector2(1.35f, 4.7f), new Vector2(0.5f, 5.5f), new Vector2(0f, 5.7f) }, 20);
            mb.M = Matrix4x4.TRS(new Vector3(0f, SpineY + 0.75f, 16.3f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(1f, 0.45f, 1.5f));
            mb.For(glass).Sphere(Vector3.zero, 1.25f, 16, 8);
            mb.M = Matrix4x4.identity;
            // Brücke mit Fensterband
            mb.For(hull).Box(new Vector3(0f, SpineY + 2.4f, 8.5f), new Vector3(5.6f, 2.2f, 4.2f));
            mb.For(hullDark).Box(new Vector3(0f, SpineY + 3.6f, 8.3f), new Vector3(6.2f, 0.25f, 4.8f));
            mb.For(glass).Box(new Vector3(0f, SpineY + 2.6f, 10.62f), new Vector3(5f, 0.75f, 0.06f));
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(glass).Box(new Vector3(s * 2.82f, SpineY + 2.6f, 8.6f), new Vector3(0.06f, 0.7f, 3.2f));
                for (int k = 0; k < 6; k++) mb.For(winGlow).Box(new Vector3(s * 2.24f, SpineY + 0.9f, -10f + k * 2.6f), new Vector3(0.06f, 0.35f, 0.9f)); // Bullaugen
            }
            // Antennenmast, Radarschüssel, Kühlrippen
            mb.For(hullDark).Cylinder(new Vector3(1.2f, SpineY + 3.7f, 7.4f), 0.07f, 3.2f, 6);
            mb.For(hullDark).Cylinder(new Vector3(-1.4f, SpineY + 3.7f, 7.8f), 0.12f, 0.8f, 6);
            mb.M = Matrix4x4.TRS(new Vector3(-1.4f, SpineY + 4.6f, 7.8f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one);
            mb.For(hull).Lathe(Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.6f, 0.12f), new Vector2(1.1f, 0.4f) }, 14, true);
            mb.M = Matrix4x4.identity;
            for (int k = 0; k < 7; k++) mb.For(dark).Box(new Vector3(0f, SpineY + 2.35f, -13f + k * 1.1f), new Vector3(3f, 0.9f, 0.1f));
            mb.For(hullDark).Box(new Vector3(0f, SpineY + 1.95f, -9.7f), new Vector3(3.1f, 0.1f, 7.8f));
            // Heck: Triebwerksblock mit drei Glocken
            mb.For(hullDark).Box(new Vector3(0f, SpineY, -17.6f), new Vector3(7.4f, 4.2f, 2.6f));
            mb.For(accent).Box(new Vector3(0f, SpineY + 2.15f, -17.6f), new Vector3(7.5f, 0.12f, 2.7f));
            foreach (var bx in BellX)
            {
                mb.M = Matrix4x4.TRS(new Vector3(bx, SpineY, -18.9f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
                mb.For(dark).Lathe(Vector3.zero, new[] { new Vector2(0.75f, 0f), new Vector2(0.85f, 0.4f), new Vector2(1.05f, 1.2f), new Vector2(1.18f, 1.9f) }, 18, true);
                mb.M = Matrix4x4.identity;
                mb.For(m.NozzleMat).CylinderZ(new Vector3(bx, SpineY, -19.05f), 0.72f, 0.1f, 16);
            }
            // Hubtriebwerke an Auslegern
            foreach (var lp in LiftPos)
            {
                float s = Mathf.Sign(lp.x);
                mb.For(hullDark).Beam(new Vector3(s * 2.1f, SpineY + 0.3f, lp.z), new Vector3(lp.x - s * 1.1f, lp.y + 0.6f, lp.z), 0.9f, 0.5f);
                mb.For(hull).Cylinder(lp + Vector3.down * 1.1f, 1.25f, 2.3f, 16);
                mb.For(dark).Cylinder(lp + Vector3.down * 1.45f, 1.05f, 0.35f, 16, true, 1.25f);
                mb.For(accent).Cylinder(lp + Vector3.up * 1.0f, 1.28f, 0.25f, 16);
                mb.For(m.NozzleMat).Cylinder(lp + Vector3.down * 1.5f, 0.8f, 0.06f, 16);
                // Positionslichter außen
                mb.For(s < 0 ? m.BeaconRed : m.BeaconGreen).Sphere(lp + new Vector3(s * 1.32f, 0.3f, 0f), 0.16f, 8, 5);
            }
            mb.For(m.BeaconWhite).Sphere(new Vector3(1.2f, SpineY + 7f, 7.4f), 0.14f, 8, 5);
            mb.For(m.BeaconWhite).Sphere(new Vector3(0f, SpineY + 2.2f, -18.9f), 0.14f, 8, 5);

            // Containergestell
            float[] rowZ = { -10.8f, -4.8f, 1.2f, 7.2f };
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(dark).Box(new Vector3(s * 5.55f, RackTop + 0.1f, -1.8f), new Vector3(0.35f, 0.35f, 25.4f));
                mb.For(dark).Box(new Vector3(s * 5.55f, RackBottom - 0.1f, -1.8f), new Vector3(0.3f, 0.3f, 25.4f));
                mb.For(yellow).Box(new Vector3(s * 5.72f, RackBottom - 0.1f, -1.8f), new Vector3(0.04f, 0.18f, 25.4f));
            }
            for (int k = 0; k <= 4; k++)
            {
                float z = -13.8f + k * 6f;
                mb.For(dark).Box(new Vector3(0f, RackTop + 0.1f, z), new Vector3(11.4f, 0.35f, 0.3f));
                for (int s = -1; s <= 1; s += 2) mb.For(dark).Box(new Vector3(s * 5.55f, (RackTop + RackBottom) * 0.5f, z), new Vector3(0.28f, RackTop - RackBottom + 0.4f, 0.28f));
            }
            // Aufhängung Rückgrat → Gestell
            for (int k = 0; k < 4; k++) mb.For(hullDark).Box(new Vector3(0f, RackTop + 0.35f, rowZ[k]), new Vector3(3.6f, 0.5f, 1.2f));

            Color[] cc =
            {
                new Color(0.88f, 0.42f, 0.14f), new Color(0.15f, 0.52f, 0.54f), new Color(0.68f, 0.17f, 0.14f), new Color(0.17f, 0.3f, 0.58f),
                new Color(0.86f, 0.68f, 0.16f), new Color(0.3f, 0.48f, 0.24f), new Color(0.52f, 0.54f, 0.56f), new Color(0.55f, 0.3f, 0.18f)
            };
            int ci = 0;
            foreach (var z in rowZ)
                for (int s = -1; s <= 1; s += 2)
                {
                    var col = Mats.Get(Mats.Opaque, cc[ci % cc.Length]);
                    var colDark = Mats.Get(Mats.Opaque, cc[ci % cc.Length] * 0.72f);
                    ci++;
                    float cx = s * 2.75f;
                    const float cw = 5.2f, ch = 4.0f, cl = 5.6f;
                    float cy = RackBottom + ch * 0.5f;
                    mb.For(col).BoxNoBottom(new Vector3(cx, cy, z), new Vector3(cw, ch, cl));
                    // Wellblech-Rippen außen und an den Enden
                    for (int r = 0; r < 11; r++)
                        mb.For(colDark).Box(new Vector3(cx + s * (cw * 0.5f + 0.03f), cy, z - cl * 0.5f + 0.35f + r * 0.49f), new Vector3(0.07f, ch - 0.35f, 0.16f));
                    for (int e = -1; e <= 1; e += 2)
                        for (int r = 0; r < 9; r++)
                            mb.For(colDark).Box(new Vector3(cx - cw * 0.5f + 0.4f + r * 0.55f, cy, z + e * (cl * 0.5f + 0.03f)), new Vector3(0.18f, ch - 0.35f, 0.07f));
                    // Eckpfosten und Kennstreifen
                    for (int a = -1; a <= 1; a += 2)
                        for (int c2 = -1; c2 <= 1; c2 += 2)
                            mb.For(dark).Box(new Vector3(cx + a * (cw * 0.5f - 0.1f), cy, z + c2 * (cl * 0.5f - 0.1f)), new Vector3(0.26f, ch + 0.05f, 0.26f));
                    mb.For(Mats.Get(Mats.Opaque, new Color(0.9f, 0.88f, 0.82f))).Box(new Vector3(cx + s * (cw * 0.5f + 0.07f), cy + 1.3f, z), new Vector3(0.03f, 0.45f, 2.2f));
                    // Innenverkleidung (sichtbar bei offener Klappe) und Innenlicht
                    mb.For(inner).Box(new Vector3(cx, RackBottom + ch - 0.08f, z), new Vector3(cw - 0.2f, 0.05f, cl - 0.2f));
                    for (int a = -1; a <= 1; a += 2)
                    {
                        mb.For(inner).Box(new Vector3(cx + a * (cw * 0.5f - 0.12f), cy, z), new Vector3(0.04f, ch - 0.1f, cl - 0.2f));
                        mb.For(inner).Box(new Vector3(cx, cy, z + a * (cl * 0.5f - 0.12f)), new Vector3(cw - 0.2f, ch - 0.1f, 0.04f));
                    }
                    mb.For(winGlow).Box(new Vector3(cx, RackBottom + ch - 0.14f, z), new Vector3(2.6f, 0.05f, 0.18f));
                    m.ContainerBottoms.Add(new Vector3(cx, RackBottom - 0.2f, z));

                    // Zwei Bodenklappen (Bombenschacht-Türen, eigene Objekte, Scharniere an den Längskanten)
                    for (int e = -1; e <= 1; e += 2)
                    {
                        var hinge = new GameObject("Klappe").transform;
                        hinge.SetParent(root, false);
                        hinge.localPosition = new Vector3(cx + e * cw * 0.5f, RackBottom, z);
                        var fb = new MultiBuilder { UsePalette = true };
                        float hw = cw * 0.5f;
                        fb.For(col).Box(new Vector3(-e * hw * 0.5f, -0.06f, 0f), new Vector3(hw, 0.12f, cl));
                        for (int k = 0; k < 3; k++)
                            fb.For(k % 2 == 0 ? yellow : dark).Box(new Vector3(-e * (0.55f + k * 0.75f), -0.13f, 0f), new Vector3(0.4f, 0.03f, cl * 0.92f));
                        fb.For(dark).Box(new Vector3(-e * 0.1f, -0.1f, 0f), new Vector3(0.2f, 0.2f, cl));
                        fb.Build("KlappeMesh", hinge, true);
                        m.Flaps.Add(hinge);
                        m.FlapSign.Add(e);
                    }
                }
            // Warnstreifen an der Gestellfront
            for (int k = 0; k < 10; k++)
                mb.For(k % 2 == 0 ? yellow : dark).BoxRot(new Vector3(-5f + k * 1.1f, RackBottom + 0.35f, 10.2f), new Vector3(0.55f, 0.5f, 0.05f), new Vector3(0f, 0f, 35f));
            // Beinaufnahmen und Scheinwerfergehäuse
            var legXZ = new[] { new Vector2(-6.1f, -12.5f), new Vector2(6.1f, -12.5f), new Vector2(-6.1f, 9.6f), new Vector2(6.1f, 9.6f) };
            foreach (var l in legXZ)
            {
                mb.For(hullDark).Box(new Vector3(l.x, RackBottom + 0.8f, l.y), new Vector3(0.9f, 2.2f, 0.9f));
                mb.For(dark).Beam(new Vector3(l.x, RackBottom + 1.6f, l.y), new Vector3(Mathf.Sign(l.x) * 5.55f, RackTop, l.y + (l.y < 0 ? 1.2f : -1.2f)), 0.25f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                mb.For(dark).Box(new Vector3(s * 1.2f, RackBottom - 0.35f, 10.4f), new Vector3(0.7f, 0.35f, 0.5f));
                mb.For(m.FloodMat).Box(new Vector3(s * 1.2f, RackBottom - 0.53f, 10.4f), new Vector3(0.55f, 0.03f, 0.38f));
                mb.For(dark).Box(new Vector3(s * 1.4f, SpineY - 0.6f, 16.2f), new Vector3(0.6f, 0.35f, 0.3f));
                mb.For(m.FloodMat).Box(new Vector3(s * 1.4f, SpineY - 0.6f, 16.36f), new Vector3(0.45f, 0.24f, 0.03f));
            }
            mb.Build("Rumpf", root, true);

            // Landebeine (Teleskop: fahren nach oben in die Aufnahme)
            foreach (var l in legXZ)
            {
                var leg = new GameObject("Landebein").transform;
                leg.SetParent(root, false);
                var basePos = new Vector3(l.x, 0f, l.y);
                leg.localPosition = basePos;
                var lb = new MultiBuilder { UsePalette = true };
                lb.For(hullDark).Cylinder(new Vector3(0f, 1.1f, 0f), 0.32f, 2.6f, 10);
                lb.For(Mats.Get(Mats.Metal, new Color(0.75f, 0.77f, 0.8f))).Cylinder(new Vector3(0f, 0.3f, 0f), 0.2f, 1.2f, 10);
                lb.For(yellow).Box(new Vector3(0f, 1.25f, 0f), new Vector3(0.72f, 0.3f, 0.72f));
                lb.For(dark).Cylinder(new Vector3(0f, 0f, 0f), 0.85f, 0.3f, 14, true, 0.55f);
                lb.For(dark).Beam(new Vector3(0f, 1.3f, 0f), new Vector3(-Mathf.Sign(l.x) * 0.2f, 3.4f, l.y < 0 ? 0.6f : -0.6f), 0.14f);
                lb.Build("BeinMesh", leg, true);
                m.Legs.Add(leg);
                m.LegBase.Add(basePos);
            }

            return m;
        }
    }
}
