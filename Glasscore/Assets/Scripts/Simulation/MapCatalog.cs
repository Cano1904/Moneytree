using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    /// <summary>
    /// ASCII arena definition. One string[] per floating layer, one char per 2m x 2m cell:
    ///   '.' empty   'S' Standard Float   'T' Tempered Safety   'R' Reinforced Bulletproof
    ///   'P' spawn pad (Reinforced Bulletproof glass flagged as a spawn point)
    /// Row 0 is the far (+Z) edge; column 0 is the left (-X) edge.
    /// </summary>
    public sealed class MapDefinition
    {
        public int Id;
        public string Name;
        public string SceneName;
        public float LayerSpacing;
        public string[][] Layers;

        public int Width => Layers[0][0].Length;
        public int Height => Layers[0].Length;
        public int LayerCount => Layers.Length;
    }

    public static class MapCatalog
    {
        public static readonly MapDefinition Greenhouse = new MapDefinition
        {
            Id = 0,
            Name = "Gewächshaus",
            SceneName = "Arena_Greenhouse",
            LayerSpacing = 5f,
            Layers = new[]
            {
                new[]
                {
                    "..RRRRRRRR..",
                    ".RSSSSSSSSR.",
                    "RSSTTSSTTSSR",
                    "RSTTSSSSTTSR",
                    "RPSSSRRSSSPR",
                    "RSSSRTTRSSSR",
                    "RSSSRTTRSSSR",
                    "RPSSSRRSSSPR",
                    "RSTTSSSSTTSR",
                    "RSSTTSSTTSSR",
                    ".RSSSSSSSSR.",
                    "..RRRRRRRR..",
                },
                new[]
                {
                    "............",
                    "............",
                    "..TTT..TTT..",
                    "..TPS..SPT..",
                    "..TSS..SST..",
                    "............",
                    "............",
                    "..TSS..SST..",
                    "..TPS..SPT..",
                    "..TTT..TTT..",
                    "............",
                    "............",
                },
            },
        };

        public static readonly MapDefinition SkylineAtrium = new MapDefinition
        {
            Id = 1,
            Name = "Skyline Atrium",
            SceneName = "Arena_SkylineAtrium",
            LayerSpacing = 6f,
            Layers = new[]
            {
                new[]
                {
                    "SSSSSRRSSSSS",
                    "SSSSSRRSSSSS",
                    "SSTTSRRSTTSS",
                    "SSTPSRRSPTSS",
                    "SSSSSRRSSSSS",
                    "RRRRRRRRRRRR",
                    "RRRRRRRRRRRR",
                    "SSSSSRRSSSSS",
                    "SSTPSRRSPTSS",
                    "SSTTSRRSTTSS",
                    "SSSSSRRSSSSS",
                    "SSSSSRRSSSSS",
                },
                new[]
                {
                    "............",
                    "............",
                    "..TTTTTTTT..",
                    "..TSSSSSST..",
                    "..TS....ST..",
                    "..TS.PP.ST..",
                    "..TS....ST..",
                    "..TSSSSSST..",
                    "..TTTTTTTT..",
                    "............",
                    "............",
                    "............",
                },
                new[]
                {
                    "............",
                    "............",
                    "............",
                    "............",
                    "....SRRS....",
                    "....RPPR....",
                    "....SSSS....",
                    "............",
                    "............",
                    "............",
                    "............",
                    "............",
                },
            },
        };

        public static readonly MapDefinition CrystalSpire = new MapDefinition
        {
            Id = 2,
            Name = "Kristallturm",
            SceneName = "Arena_CrystalSpire",
            LayerSpacing = 5f,
            Layers = new[]
            {
                new[]
                {
                    "TTTTSSSSTTTT",
                    "TPSSSSSSSSPT",
                    "TSSSRSSRSSST",
                    "TSSSSSSSSSST",
                    "SSRSSTTSSRSS",
                    "SSSSTRRTSSSS",
                    "SSSSTRRTSSSS",
                    "SSRSSTTSSRSS",
                    "TSSSSSSSSSST",
                    "TSSSRSSRSSST",
                    "TPSSSSSSSSPT",
                    "TTTTSSSSTTTT",
                },
                new[]
                {
                    "............",
                    "............",
                    "..RSSSSSSR..",
                    "..SPTTTTPS..",
                    "..STSSSSTS..",
                    "..STS..STS..",
                    "..STS..STS..",
                    "..STSSSSTS..",
                    "..SPTTTTPS..",
                    "..RSSSSSSR..",
                    "............",
                    "............",
                },
                new[]
                {
                    "............",
                    "............",
                    "............",
                    "............",
                    "....TTTT....",
                    "....TRRT....",
                    "....TRRT....",
                    "....TTTT....",
                    "............",
                    "............",
                    "............",
                    "............",
                },
            },
        };

        public static readonly MapDefinition[] All = { Greenhouse, SkylineAtrium, CrystalSpire };

        public static MapDefinition Get(int id) => All[GcMath.Clamp(id, 0, All.Length - 1)];
    }

    /// <summary>
    /// Compact tile table built from a <see cref="MapDefinition"/>. Tile IDs (the "Network_ID" of each
    /// tile) are dense indices assigned in layer → row → column order, identical on server and clients.
    /// </summary>
    public sealed class TileLayout
    {
        public const int MaxTiles = 1024;

        public readonly MapDefinition Map;
        public readonly int Count;
        public readonly GlassType[] Types;
        public readonly bool[] IsSpawnPad;
        public readonly int[] Layer;
        public readonly int[] Column;
        public readonly int[] Row;
        public readonly Vec3[] Centers;
        private readonly int[] _cellToId; // [layer, row, col] → id or -1

        public TileLayout(MapDefinition map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            int w = map.Width, h = map.Height, l = map.LayerCount;
            _cellToId = new int[w * h * l];

            var types = new List<GlassType>();
            var pads = new List<bool>();
            var layer = new List<int>();
            var col = new List<int>();
            var row = new List<int>();
            var centers = new List<Vec3>();

            for (int li = 0; li < l; li++)
            {
                if (map.Layers[li].Length != h) throw new FormatException($"{map.Name}: layer {li} has {map.Layers[li].Length} rows, expected {h}.");
                for (int r = 0; r < h; r++)
                {
                    string line = map.Layers[li][r];
                    if (line.Length != w) throw new FormatException($"{map.Name}: layer {li} row {r} has {line.Length} columns, expected {w}.");
                    for (int c = 0; c < w; c++)
                    {
                        int cell = (li * h + r) * w + c;
                        GlassType t = Parse(line[c], out bool pad);
                        if (t == GlassType.None)
                        {
                            _cellToId[cell] = -1;
                            continue;
                        }
                        _cellToId[cell] = types.Count;
                        types.Add(t);
                        pads.Add(pad);
                        layer.Add(li);
                        col.Add(c);
                        row.Add(r);
                        centers.Add(new Vec3(
                            (c - (w - 1) * 0.5f) * GlassCatalog.TileSize,
                            li * map.LayerSpacing,
                            ((h - 1) * 0.5f - r) * GlassCatalog.TileSize));
                    }
                }
            }

            Count = types.Count;
            if (Count > MaxTiles) throw new FormatException($"{map.Name}: {Count} tiles exceeds MaxTiles {MaxTiles}.");
            Types = types.ToArray();
            IsSpawnPad = pads.ToArray();
            Layer = layer.ToArray();
            Column = col.ToArray();
            Row = row.ToArray();
            Centers = centers.ToArray();
        }

        private static GlassType Parse(char ch, out bool spawnPad)
        {
            spawnPad = false;
            switch (ch)
            {
                case 'S': return GlassType.Standard;
                case 'T': return GlassType.Tempered;
                case 'R': return GlassType.Reinforced;
                case 'P': spawnPad = true; return GlassType.Reinforced;
                case '.': return GlassType.None;
                default: throw new FormatException($"Unknown tile char '{ch}'.");
            }
        }

        public int IdAt(int layer, int row, int column)
        {
            if (layer < 0 || layer >= Map.LayerCount || row < 0 || row >= Map.Height || column < 0 || column >= Map.Width) return -1;
            return _cellToId[(layer * Map.Height + row) * Map.Width + column];
        }

        public int GetNeighbors(int tileId, int[] buffer)
        {
            int l = Layer[tileId], r = Row[tileId], c = Column[tileId];
            int n = 0;
            int id;
            if ((id = IdAt(l, r - 1, c)) >= 0) buffer[n++] = id;
            if ((id = IdAt(l, r + 1, c)) >= 0) buffer[n++] = id;
            if ((id = IdAt(l, r, c - 1)) >= 0) buffer[n++] = id;
            if ((id = IdAt(l, r, c + 1)) >= 0) buffer[n++] = id;
            return n;
        }

        public float MaxIntegrity(int tileId) => GlassCatalog.Get(Types[tileId]).MaxIntegrity;

        /// <summary>Horizontal distance of a tile centre from the arena's vertical axis.</summary>
        public float RadialDistance(int tileId) => Centers[tileId].XZ.Length;

        public int SpawnPadCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (IsSpawnPad[i]) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Array-backed tile integrity store. The server's authoritative copy lives in GameWorld; each
    /// client keeps a replica fed by TileState/TileDelta messages, used for predicted collision.
    /// </summary>
    public sealed class TileGridModel : ITileStore, ITileQuery
    {
        private readonly TileLayout _layout;
        private readonly float[] _integrity;
        private readonly bool[] _dirtyFlags;
        private readonly List<int> _dirty = new List<int>();

        public TileGridModel(TileLayout layout)
        {
            _layout = layout;
            _integrity = new float[layout.Count];
            _dirtyFlags = new bool[layout.Count];
            ResetAll();
        }

        public TileLayout Layout => _layout;
        public int TileCount => _layout.Count;
        public IReadOnlyList<int> DirtyTiles => _dirty;

        public void ResetAll()
        {
            for (int i = 0; i < _layout.Count; i++) _integrity[i] = _layout.MaxIntegrity(i);
            ClearDirty();
        }

        public GlassType GetGlassType(int tileId) => _layout.Types[tileId];
        public float GetIntegrity(int tileId) => _integrity[tileId];
        public bool IsSolid(int tileId) => _integrity[tileId] > 0f;
        public int GetNeighbors(int tileId, int[] buffer) => _layout.GetNeighbors(tileId, buffer);

        public void SetIntegrity(int tileId, float integrity)
        {
            if (integrity < 0f) integrity = 0f;
            if (_integrity[tileId] == integrity) return;
            _integrity[tileId] = integrity;
            if (!_dirtyFlags[tileId])
            {
                _dirtyFlags[tileId] = true;
                _dirty.Add(tileId);
            }
        }

        public void ClearDirty()
        {
            for (int i = 0; i < _dirty.Count; i++) _dirtyFlags[_dirty[i]] = false;
            _dirty.Clear();
        }

        /// <summary>Wire format: centi-HP rounded UP so any intact tile stays non-zero on clients.</summary>
        public static ushort Quantize(float integrity) =>
            (ushort)Math.Min(ushort.MaxValue, Math.Ceiling(Math.Max(0f, integrity) * 100.0 - 1e-6));

        public static float Dequantize(ushort centi) => centi / 100f;
    }
}
