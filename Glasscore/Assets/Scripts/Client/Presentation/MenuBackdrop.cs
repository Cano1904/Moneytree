using System.Collections.Generic;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Main-menu background: a single neon-framed glass pane that shatters in slow motion, hangs in the
    /// air and rewinds back together, looping forever. The loop is evaluated from unscaled time on every
    /// rendered frame (V-Synced to the display), so it stays perfectly smooth regardless of frame rate.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        private const float Cycle = 8f;
        private const float ShatterAt = 1.6f;
        private const float RewindAt = 5.6f;

        private readonly List<Transform> _shards = new List<Transform>();
        private readonly List<Vector3> _rest = new List<Vector3>();
        private readonly List<Vector3> _velocity = new List<Vector3>();
        private readonly List<Vector3> _spin = new List<Vector3>();
        private GameObject _pane;
        private Transform _pivot;
        private bool _playedShatter;
        private Camera _camera;

        public void Init(Camera camera)
        {
            _camera = camera;
            _pivot = new GameObject("PanePivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = new Vector3(1.4f, 0f, 0f);
            _pivot.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f);

            _pane = new GameObject("Pane");
            _pane.transform.SetParent(_pivot, false);
            _pane.transform.localScale = new Vector3(2f, 0.1f, 2f);
            _pane.AddComponent<MeshFilter>().sharedMesh = Meshes.Cube;
            _pane.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glass;

            var frame = new GameObject("NeonFrame");
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = _pivot.localPosition;
            frame.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            frame.AddComponent<MeshFilter>().sharedMesh = Meshes.Ring(1.75f, 0.05f, 64);
            frame.AddComponent<MeshRenderer>().sharedMaterial = Mats.Neon(new Color(0f, 1f, 1f, 0.8f), 2.5f);

            var sites = VoronoiFracture2D.GenerateSites(20240926UL, new Vec2(0.25f, 0.1f), 1f, 1f, 48, 0.8f);
            var cells = VoronoiFracture2D.ComputeCells(sites, 1f, 1f);
            var rng = new DeterministicRandom(77);
            foreach (var cell in cells)
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(_pivot, false);
                var mesh = new Mesh();
                Meshes.Fill(mesh, PrismMeshBuilder.Build(cell.Polygon, GlassCatalog.TileThickness, cell.Centroid, 1f));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Shard;
                var rest = new Vector3(cell.Centroid.X, 0f, cell.Centroid.Y);
                go.transform.localPosition = rest;
                _shards.Add(go.transform);
                _rest.Add(rest);
                Vector3 outward = (rest - new Vector3(0.25f, 0f, 0.1f)).normalized;
                _velocity.Add(outward * rng.Range(0.3f, 1.1f) + new Vector3(0f, rng.Range(0.2f, 1.2f), 0f)); // local +Y faces the camera
                _spin.Add(new Vector3(rng.Range(-60f, 60f), rng.Range(-60f, 60f), rng.Range(-60f, 60f)));
                go.SetActive(false);
            }
        }

        private void OnEnable() => _playedShatter = false;

        private void LateUpdate()
        {
            if (_camera == null) return;
            float t = (float)(Time.unscaledTimeAsDouble % Cycle);
            _camera.transform.position = new Vector3(0f, 0.2f, -5.2f) + new Vector3(Mathf.Sin(t / Cycle * Mathf.PI * 2f) * 0.25f, 0f, 0f);
            _camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.9f, 0f, 0f) - _camera.transform.position);
            _camera.fieldOfView = 55f;
            _pivot.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 0.3f) * 8f, 0f);

            bool broken = t >= ShatterAt;
            _pane.SetActive(!broken);
            if (!broken)
            {
                _playedShatter = false;
                foreach (var s in _shards) s.gameObject.SetActive(false);
                return;
            }

            if (!_playedShatter)
            {
                _playedShatter = true;
                AudioService.Instance?.Play(AudioService.ShatterSmall, 0.35f, 0.8f);
            }

            // Slow-motion flight, then an eased rewind back into the intact pane.
            float flight;
            if (t < RewindAt) flight = (t - ShatterAt) * 0.35f;
            else
            {
                float k = Mathf.Clamp01((t - RewindAt) / (Cycle - RewindAt));
                flight = (RewindAt - ShatterAt) * 0.35f * (1f - k * k * (3f - 2f * k));
            }

            for (int i = 0; i < _shards.Count; i++)
            {
                Transform s = _shards[i];
                if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                s.localPosition = _rest[i] + _velocity[i] * flight;
                s.localRotation = Quaternion.Euler(_spin[i] * flight);
            }
        }
    }
}
