using System;

namespace RePlanet.Core
{
    /// <summary>Deterministische Höhenfunktion je Planet. Client (Darstellung, Bewegung) und Server nutzen dieselbe Funktion.</summary>
    public static class Terrain
    {
        public static readonly float[] RoadZ = { -75f, -25f, 25f, 75f, 125f };
        public static readonly float[] RoadX = { -100f, -50f, 50f, 100f };

        public static V3 ProjectSite(string planet, int area)
        {
            switch (planet)
            {
                case "pelagia":
                    if (area == 0) return new V3(-40, 0, -85);
                    if (area == 1) return new V3(0, 0, 10);
                    return new V3(0, 0, 108);
                default:
                    if (area == 0) return new V3(-40, 0, -88);
                    if (area == 1) return new V3(0, 0, 8);
                    return new V3(0, 0, 108);
            }
        }

        public static float WaterLevel(string planet)
        {
            return planet == "pelagia" ? 0f : -100f;
        }

        static bool UsesGridRoads(string planet) { return planet == "terra" || planet == "nivalis"; }

        /// <summary>0 = vollständig eingeebnet (Straße/Basis), 1 = freies Gelände.</summary>
        public static float FlatMask(string planet, float x, float z)
        {
            float d = 1e9f;
            // Stützpunkt
            float bx = Math.Max(0, Math.Abs(x) - 36f), bz = Math.Max(0, Math.Max(-152f - z, z + 92f));
            d = Math.Min(d, (float)Math.Sqrt(bx * bx + bz * bz));
            // Hauptstraße
            d = Math.Min(d, Math.Max(0, Math.Abs(x) - 9f));
            if (UsesGridRoads(planet) || planet == "pyra")
            {
                foreach (var rz in RoadZ) d = Math.Min(d, Math.Max(0, Math.Abs(z - rz) - 6f));
            }
            if (UsesGridRoads(planet))
            {
                foreach (var rx in RoadX) d = Math.Min(d, Math.Max(0, Math.Abs(x - rx) - 5f));
            }
            for (int a = 0; a < 3; a++)
            {
                var p = ProjectSite(planet, a);
                float dx = x - p.x, dz = z - p.z;
                d = Math.Min(d, Math.Max(0, (float)Math.Sqrt(dx * dx + dz * dz) - 18f));
            }
            return M.Smooth(d / 10f);
        }

        public static float HeightAt(string planet, float x, float z)
        {
            switch (planet)
            {
                case "terra":
                    {
                        if (z < 50f) return 0f;
                        float n = Noise.Fbm(x * 0.02f, z * 0.02f, 11021, 3);
                        float raw = Math.Max(0f, (n - 0.45f) * 7f) * M.Smooth((z - 50f) / 12f);
                        return raw * FlatMask(planet, x, z);
                    }
                case "pyra":
                    {
                        float n = Noise.Fbm(x * 0.011f, z * 0.011f, 22877, 4);
                        float raw = Math.Max(0f, (n - 0.38f) * 15f);
                        return raw * FlatMask(planet, x, z);
                    }
                case "nivalis":
                    {
                        float n = Noise.Fbm(x * 0.014f, z * 0.014f, 44753, 4);
                        float raw = Math.Max(0f, (n - 0.3f) * 6f);
                        return raw * FlatMask(planet, x, z);
                    }
                case "pelagia":
                    return PelagiaHeight(x, z);
            }
            return 0f;
        }

        static float PelagiaHeight(float x, float z)
        {
            float n = Noise.Fbm(x * 0.018f, z * 0.018f, 33419, 4);
            const float land = 1.2f;
            float h;
            // Hafen (Bereich 0): Festland mit Hafenbecken
            float harbor = land + (n - 0.5f) * 0.8f;
            float basinX = M.Smooth(Math.Min((x - 40f) / 5f, (112f - x) / 5f));
            float basinZ = M.Smooth(Math.Min((z + 130f) / 5f, (-62f - z) / 5f));
            float basin = Math.Min(basinX, basinZ);
            harbor = M.Lerp(harbor, -5.5f, M.Clamp01(basin));

            // Küstensiedlung (Bereich 1): Inseln mit Damm entlang der Hauptstraße
            float islands = M.Clamp((n - 0.5f) * 24f, -5f, 3.2f);
            float causeway = M.Smooth(1f - (Math.Abs(x) - 7f) / 4f);
            islands = M.Lerp(islands, land, causeway);

            // Lagune (Bereich 2): tief, mit Zentralinsel und Randinseln
            float dc = (float)Math.Sqrt(x * x + (z - 108f) * (z - 108f));
            float lagoon = -11f + Math.Max(0f, (n - 0.6f) * 40f);
            lagoon = Math.Min(lagoon, 2.5f);
            lagoon = M.Lerp(land + 0.3f, lagoon, M.Smooth((dc - 16f) / 6f));
            if (Math.Abs(x) > 128f || z > 138f) lagoon = M.Lerp(lagoon, 2.0f, M.Smooth((Math.Max(Math.Abs(x) - 128f, z - 138f)) / 6f));

            // Kanäle an den Bereichsgrenzen
            if (z < -62f) h = harbor;
            else if (z < -54f) h = M.Lerp(harbor, -3.5f, M.Smooth((z + 62f) / 8f));
            else if (z < -46f) h = -3.5f;
            else if (z < -38f) h = M.Lerp(-3.5f, islands, M.Smooth((z + 46f) / 8f));
            else if (z < 38f) h = islands;
            else if (z < 44f) h = M.Lerp(islands, -7f, M.Smooth((z - 38f) / 6f));
            else if (z < 58f) h = -7f;
            else if (z < 64f) h = M.Lerp(-7f, lagoon, M.Smooth((z - 58f) / 6f));
            else h = lagoon;

            // Stützpunkt eben
            float bx = Math.Max(0, Math.Abs(x) - 36f), bz = Math.Max(0, Math.Max(-152f - z, z + 92f));
            float bd = (float)Math.Sqrt(bx * bx + bz * bz);
            h = M.Lerp(land, h, M.Smooth(bd / 8f));
            // Projektplätze
            for (int a = 0; a < 2; a++)
            {
                var p = ProjectSite("pelagia", a);
                float dx = x - p.x, dz = z - p.z;
                float pd = Math.Max(0, (float)Math.Sqrt(dx * dx + dz * dz) - 14f);
                h = M.Lerp(land, h, M.Smooth(pd / 6f));
            }
            return h;
        }

        public static bool IsWater(string planet, float x, float z, float minDepth)
        {
            return HeightAt(planet, x, z) < WaterLevel(planet) - minDepth;
        }
    }
}
