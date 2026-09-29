using System.Collections.Generic;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Builds the floating glass-tile arena from the replicated <see cref="TileGridModel"/> and keeps each
    /// tile's crack / spiderweb / erosion-warning look in sync with its integrity.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        private TileLayout _layout;
        private TileGridModel _tiles;
        private GameObject[] _tileObjects;
        private MeshRenderer[] _renderers;
        private float[] _shownIntegrity;
        private bool[] _destabilized;
        private float _erosionMaxRadius;
        private readonly List<int> _waveScratch = new List<int>();
        private MaterialPropertyBlock _mpb;
        private float _nextCrackSound;

        public TileLayout Layout => _layout;
        public int MatchSeed { get; private set; }

        public void Build(TileLayout layout, TileGridModel tiles, int matchSeed)
        {
            _layout = layout;
            _tiles = tiles;
            MatchSeed = matchSeed;
            _mpb = new MaterialPropertyBlock();
            _tileObjects = new GameObject[layout.Count];
            _renderers = new MeshRenderer[layout.Count];
            _shownIntegrity = new float[layout.Count];
            _destabilized = new bool[layout.Count];
            _erosionMaxRadius = ErosionPlanner.MaxRadius(layout);

            Material glass = Mats.Glass;
            Mesh cube = Meshes.Cube;
            Mesh ring = Meshes.Ring(0.75f, 0.08f);
            Material padMat = Mats.Neon(new Color(0f, 1f, 1f, 0.8f), 2f);

            for (int i = 0; i < layout.Count; i++)
            {
                var go = new GameObject($"Tile_{i}_{layout.Types[i]}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = layout.Centers[i].ToUnity();
                go.transform.localScale = new Vector3(GlassCatalog.TileSize, GlassCatalog.TileThickness, GlassCatalog.TileSize);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = glass;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<BoxCollider>(); // lets local shard physics land on intact glass
                _tileObjects[i] = go;
                _renderers[i] = mr;
                _shownIntegrity[i] = -1f;

                if (layout.IsSpawnPad[i])
                {
                    var pad = new GameObject("SpawnPad");
                    pad.transform.SetParent(transform, false);
                    pad.transform.localPosition = layout.Centers[i].ToUnity() + Vector3.up * 0.06f;
                    pad.AddComponent<MeshFilter>().sharedMesh = ring;
                    pad.AddComponent<MeshRenderer>().sharedMaterial = padMat;
                    pad.transform.SetParent(go.transform, true);
                }
            }

            BuildScenery(layout);
            Refresh(force: true);
        }

        private void BuildScenery(TileLayout layout)
        {
            float radius = _erosionMaxRadius + 18f;
            var rng = new DeterministicRandom((ulong)(MatchSeed + 99));
            Material monolith = Mats.Glass;
            for (int i = 0; i < 26; i++)
            {
                float a = rng.NextFloat() * Mathf.PI * 2f;
                float r = radius + rng.NextFloat() * 40f;
                var go = new GameObject("Monolith");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(a) * r, rng.Range(-20f, 25f), Mathf.Sin(a) * r);
                go.transform.localScale = new Vector3(rng.Range(2f, 6f), rng.Range(8f, 30f), rng.Range(0.2f, 0.6f));
                go.transform.localRotation = Quaternion.Euler(rng.Range(-8f, 8f), a * Mathf.Rad2Deg + 90f, rng.Range(-8f, 8f));
                go.AddComponent<MeshFilter>().sharedMesh = Meshes.Cube;
                go.AddComponent<MeshRenderer>().sharedMaterial = monolith;
            }

            // Glowing void grid far below: shows where the kill plane is.
            var grid = new GameObject("VoidGrid");
            grid.transform.SetParent(transform, false);
            grid.transform.localPosition = new Vector3(0f, -PlayerRules.KillPlaneBelowLowestLayer - 2f, 0f);
            grid.AddComponent<MeshFilter>().sharedMesh = Meshes.Ring(radius, 0.4f, 96);
            grid.AddComponent<MeshRenderer>().sharedMaterial = Mats.Neon(new Color(1f, 0.2f, 0.4f, 0.5f), 2f);
            for (int k = 1; k < 5; k++)
            {
                var r = new GameObject("VoidRing");
                r.transform.SetParent(grid.transform, false);
                r.AddComponent<MeshFilter>().sharedMesh = Meshes.Ring(radius * k / 5f, 0.25f, 96);
                r.AddComponent<MeshRenderer>().sharedMaterial = Mats.Neon(new Color(1f, 0.2f, 0.4f, 0.25f), 1.5f);
            }
        }

        /// <summary>Client-side mirror of the server's erosion wave so the doomed ring pulses red.</summary>
        public void OnErosionWave(int waveIndex)
        {
            if (_layout == null) return;
            ErosionPlanner.CollectWave(_layout, _tiles, _destabilized, _erosionMaxRadius, waveIndex, _waveScratch);
            Refresh(force: true);
        }

        public Vector3 TileCenter(int tileId) => _layout.Centers[tileId].ToUnity();

        public void HideTile(int tileId)
        {
            if (_tileObjects != null && tileId >= 0 && tileId < _tileObjects.Length) _tileObjects[tileId].SetActive(false);
        }

        private void LateUpdate() => Refresh(force: false);

        private void Refresh(bool force)
        {
            if (_tiles == null) return;
            for (int i = 0; i < _layout.Count; i++)
            {
                float integrity = _tiles.GetIntegrity(i);
                if (!force && integrity == _shownIntegrity[i] && !_destabilized[i]) continue;

                bool damaged = _shownIntegrity[i] > 0f && integrity < _shownIntegrity[i] && integrity > 0f;
                _shownIntegrity[i] = integrity;

                if (integrity <= 0f)
                {
                    if (_tileObjects[i].activeSelf) _tileObjects[i].SetActive(false);
                    continue;
                }
                if (!_tileObjects[i].activeSelf) _tileObjects[i].SetActive(true);

                GlassType type = _layout.Types[i];
                GlassSpec spec = GlassCatalog.Get(type);
                float frac = integrity / spec.MaxIntegrity;
                TileVisualState state = GlassCatalog.VisualState(type, integrity);

                _mpb.Clear();
                _mpb.SetColor("_Color", Mats.GlassTint(type));
                _mpb.SetColor("_EdgeColor", Mats.GlassEdge(type));
                _mpb.SetFloat("_Crack", state == TileVisualState.Intact ? (1f - frac) * 0.35f : Mathf.Clamp01(1.1f - frac));
                _mpb.SetFloat("_Spiderweb", state == TileVisualState.Spiderweb ? 1f : 0f);
                _mpb.SetFloat("_Seed", i * 0.137f);
                _mpb.SetFloat("_Warning", _destabilized[i] ? 1f : 0f);
                _renderers[i].SetPropertyBlock(_mpb);

                if (damaged && Time.time >= _nextCrackSound)
                {
                    _nextCrackSound = Time.time + 0.05f;
                    AudioService.Instance?.PlayAt(AudioService.CrackSmall, _layout.Centers[i].ToUnity(), 0.6f, 0.9f + 0.3f * frac);
                }
            }
        }
    }
}
