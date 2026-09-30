using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>Achsenparalleler Quader für Kollision und Darstellung.</summary>
    public class Box
    {
        public float Cx, Cz, Hx, Hz, Y0, H;
        public string Kind;
        public int Style, Area = -1;
        public bool Solid = true;
        /// <summary>-1 = immer; sonst nur solange das Tor geschlossen ist.</summary>
        public int Gate = -1;
        /// <summary>-1 = immer; 0/1 = Dünen-Set auf PYRA (wechselt mit Sandstürmen).</summary>
        public int DuneSet = -1;
        public uint Color;
        public bool Contains(float x, float z, float margin)
        {
            return x > Cx - Hx - margin && x < Cx + Hx + margin && z > Cz - Hz - margin && z < Cz + Hz + margin;
        }
    }

    /// <summary>Reine Dekoration (keine Kollision).</summary>
    public class Prop
    {
        public string Kind;
        public V3 Pos;
        public float Rot, Scale = 1f;
        public int Area, Style;
        /// <summary>Wird bei Abschluss dieses Projekts aktiviert (Licht an, Wasser fließt …).</summary>
        public string LitBy;
    }

    public class TrashObj
    {
        public int Id;
        public string Type;
        public V3 Pos;
        public float Rot, Scale = 1f;
        public int Area, Zone = -1, Gate = -1;
        public bool Underwater, Frozen;
        public TrashType Def { get { return GameData.Trash[Type]; } }
    }

    public class ZoneDef
    {
        public int Index, Area;
        public string Name;
        public V3 Center;
        public float Radius;
        public List<int> Objects = new List<int>();
    }

    public class Spot
    {
        public string Id, Kind, Name;
        public V3 Pos;
        public int Area;
        public float Yaw;
    }

    public class GateLayout
    {
        public int Index, FromArea, ToArea;
        public Box Blocker;
        public List<int> Objects = new List<int>();
        public string Hint;
    }

    public class Mound { public V3 Pos; public float Radius, Height; public int Area; }

    /// <summary>
    /// Begehbarer Schutzraum (Hangar im Hauptgebäude, Laderaum des Transportschiffs): feste Wände als Kollisionsboxen,
    /// freie Innenfläche und freie Eingangsöffnung (Tor bzw. Heckrampe). Wer darin steht, ist vor Nacht und Sturm
    /// geschützt, kann schlafen, aber keine Werkzeuge benutzen.
    /// </summary>
    public class ShelterRoom
    {
        /// <summary><see cref="Rules.ShelterHangar"/> oder <see cref="Rules.ShelterShip"/>.</summary>
        public int Kind;
        public string Id, Name;
        /// <summary>Freie Innenfläche; Y0 = Boden, H = lichte Höhe.</summary>
        public Box Inner;
        /// <summary>Eingangsöffnung (Toröffnung in der Wand bzw. Rampe vor dem Heck).</summary>
        public Box Door;
        /// <summary>Mitte des Innenraums (Schlafplatz) und Vorplatz vor dem Eingang.</summary>
        public V3 Spot, Outside;
        /// <summary>Richtung von innen nach außen durch den Eingang (Einheitsvektor in XZ).</summary>
        public float OutX, OutZ;

        public bool Contains(V3 p, float margin = 0f)
        {
            return Inner.Contains(p.x, p.z, margin) && p.y > Inner.Y0 - 1.2f && p.y < Inner.Y0 + Inner.H;
        }

        /// <summary>Punkt in die Innenfläche ziehen (Abstand <paramref name="inset"/> zu den Wänden).</summary>
        public V3 Clamp(V3 p, float inset)
        {
            float hx = Math.Max(0f, Inner.Hx - inset), hz = Math.Max(0f, Inner.Hz - inset);
            return new V3(Math.Max(Inner.Cx - hx, Math.Min(Inner.Cx + hx, p.x)), p.y, Math.Max(Inner.Cz - hz, Math.Min(Inner.Cz + hz, p.z)));
        }
    }

    /// <summary>Begehbare Fläche über dem Gelände (Laderaumboden, Rampe). Höhe steigt linear von (X0|Y0) nach (X1|Y1) bzw. entlang z.</summary>
    public class FloorPatch
    {
        public float X0, X1, Z0, Z1, Y0, Y1;
        /// <summary>0 = Anstieg entlang x (von X0 nach X1), 1 = entlang z (von Z0 nach Z1).</summary>
        public int Axis;
        public bool Contains(float x, float z) { return x >= Math.Min(X0, X1) && x <= Math.Max(X0, X1) && z >= Math.Min(Z0, Z1) && z <= Math.Max(Z0, Z1); }
        public float HeightAt(float x, float z)
        {
            float t = Axis == 0 ? (X1 == X0 ? 1f : (x - X0) / (X1 - X0)) : (Z1 == Z0 ? 1f : (z - Z0) / (Z1 - Z0));
            t = Math.Max(0f, Math.Min(1f, t));
            return Y0 + (Y1 - Y0) * t;
        }
    }

    public class BaseLayout
    {
        public V3 Center, Spawn, DropZone, GarageSpot, BoatSpot, ShipPad;
        /// <summary>Befahrbare Schutzräume: Hangar im Hauptgebäude (Rolltor) und Laderaum des Transportschiffs (Heckrampe).</summary>
        public ShelterRoom Hangar, Ship;
        public readonly List<ShelterRoom> Rooms = new List<ShelterRoom>();
        /// <summary>Blickrichtung der Schiffsnase (Grad um die Hochachse, 90 = +X). Darstellung und Anflugszene richten sich danach.</summary>
        public float ShipYaw = 90f;
        /// <summary>Höhe des Laderaumbodens (Welt-y).</summary>
        public float ShipFloorY;
        public float DropRadius = 7f;
        public Dictionary<string, V3> Stations = new Dictionary<string, V3>();
        public float GridX0, GridZ0, Cell = 2f;
        public int GridW, GridH;
        public float MinX, MaxX, MinZ, MaxZ;
        public bool InBase(float x, float z) { return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ; }
        public V3 CellCenter(int gx, int gz, int w, int h)
        {
            return new V3(GridX0 + (gx + w * 0.5f) * Cell, 0, GridZ0 + (gz + h * 0.5f) * Cell);
        }
    }

    public class PlanetLayout
    {
        public PlanetDef Def;
        public string Id { get { return Def.Id; } }
        public List<TrashObj> Trash = new List<TrashObj>();
        public List<Box> Colliders = new List<Box>();
        public List<Prop> Props = new List<Prop>();
        public List<ZoneDef> Zones = new List<ZoneDef>();
        public GateLayout[] Gates = new GateLayout[2];
        public List<Spot> Repairs = new List<Spot>();
        public List<Spot> Eco = new List<Spot>();
        public List<Spot> LoreSpots = new List<Spot>();
        public List<Spot> Viewpoints = new List<Spot>();
        public List<Spot> Shelters = new List<Spot>();
        /// <summary>Fundorte defekter Helferroboter (einer je Bereich; reparierbar, siehe <see cref="HelperBot"/>).</summary>
        public List<Spot> Bots = new List<Spot>();
        public V3[] ProjectSites = new V3[3];
        public List<Mound> Mounds = new List<Mound>();
        public BaseLayout Base = new BaseLayout();
        public float[] AreaWeight = new float[3];
        public int[] AreaCount = new int[3];
        public List<float[]> Roads = new List<float[]>(); // x0,z0,x1,z1,width
        /// <summary>Begehbare Flächen über dem Gelände (Schiffsrampe, Laderaumboden).</summary>
        public List<FloorPatch> Floors = new List<FloorPatch>();
        public const float Half = 150f;

        /// <summary>Bodenhöhe für Bewegung: Gelände oder – falls höher – eine begehbare Fläche (Rampe, Laderaum).</summary>
        public float GroundAt(float x, float z)
        {
            float g = Terrain.HeightAt(Id, x, z);
            for (int i = 0; i < Floors.Count; i++)
            {
                var f = Floors[i];
                if (!f.Contains(x, z)) continue;
                float h = f.HeightAt(x, z);
                if (h > g) g = h;
            }
            return g;
        }

        public static int AreaOf(float z) { return z < -50f ? 0 : (z < 50f ? 1 : 2); }

        public TrashObj Get(int id) { return id >= 0 && id < Trash.Count ? Trash[id] : null; }

        public Spot FindSpot(string id)
        {
            foreach (var s in Repairs) if (s.Id == id) return s;
            foreach (var s in Eco) if (s.Id == id) return s;
            foreach (var s in LoreSpots) if (s.Id == id) return s;
            foreach (var s in Viewpoints) if (s.Id == id) return s;
            foreach (var s in Shelters) if (s.Id == id) return s;
            return null;
        }

        // Räumliches Raster für schnelle Kollisionsabfragen
        List<Box>[] grid;
        const float GridCell = 16f;
        const int GridN = 20; // 320 m / 16
        public void BuildGrid()
        {
            grid = new List<Box>[GridN * GridN];
            foreach (var b in Colliders)
            {
                if (!b.Solid) continue;
                int x0 = GIdx(b.Cx - b.Hx), x1 = GIdx(b.Cx + b.Hx), z0 = GIdx(b.Cz - b.Hz), z1 = GIdx(b.Cz + b.Hz);
                for (int gx = x0; gx <= x1; gx++)
                    for (int gz = z0; gz <= z1; gz++)
                    {
                        int k = gz * GridN + gx;
                        if (grid[k] == null) grid[k] = new List<Box>();
                        grid[k].Add(b);
                    }
            }
        }
        static int GIdx(float v) { return M.Clamp((int)((v + 160f) / GridCell), 0, GridN - 1); }

        static readonly List<Box> empty = new List<Box>();
        public void Query(float x, float z, float r, List<Box> result)
        {
            result.Clear();
            if (grid == null) BuildGrid();
            int x0 = GIdx(x - r), x1 = GIdx(x + r), z0 = GIdx(z - r), z1 = GIdx(z + r);
            for (int gx = x0; gx <= x1; gx++)
                for (int gz = z0; gz <= z1; gz++)
                {
                    var l = grid[gz * GridN + gx];
                    if (l == null) continue;
                    foreach (var b in l) if (!result.Contains(b)) result.Add(b);
                }
        }

        public bool BlockedStatic(float x, float z, float margin)
        {
            var tmp = new List<Box>();
            Query(x, z, margin + 1, tmp);
            foreach (var b in tmp) if (b.Contains(x, z, margin)) return true;
            return false;
        }
    }
}
