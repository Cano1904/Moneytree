using System.Collections.Generic;
using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Everything visible during a match: arena, avatars, projectiles, effects, first-person camera and
    /// the event feed the HUD reads. Created on MatchStart, destroyed on ReturnToLobby.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public struct FeedEntry
        {
            public float Time;
            public string Text;
            public Color Color;
        }

        private sealed class Avatar
        {
            public GameObject Root;
            public Transform WeaponPivot;
            public MeshRenderer Weapon;
            public GameObject AnchorRing;
            public TrailRenderer Trail;
            public Renderer[] BodyRenderers;
            public byte ShownSkin = 255;
            public byte ShownTrail = 255;
            public bool WasAlive;
        }

        private sealed class LocalProjectile
        {
            public GameObject Go;
            public Vec3 Origin;
            public Vec3 Velocity;
            public float Age;
            public byte Weapon;
        }

        private GameApp _app;
        private GameClient _client;
        private ArenaView _arena;
        private readonly Avatar[] _avatars = new Avatar[GameWorld.MaxPlayers];
        private readonly List<GameObject> _projectilePool = new List<GameObject>();
        private readonly List<LocalProjectile> _localProjectiles = new List<LocalProjectile>();
        private readonly Stack<GameObject> _localPool = new Stack<GameObject>();
        private ParticleSystem _sparks;
        private Transform _viewmodel;
        private MeshRenderer _viewmodelRenderer;
        private float _viewKick;
        private float _trauma;
        private float _fovKick;
        private float _spectateAngle;

        public readonly List<FeedEntry> Feed = new List<FeedEntry>();
        public string Banner { get; private set; }
        public float BannerUntil { get; private set; }
        public Color BannerColor { get; private set; } = Color.white;
        public float HitMarkerUntil { get; private set; }
        public float DamageFlashUntil { get; private set; }
        public float LocalRespawnAt { get; private set; }
        public int LastKillerSlot { get; private set; } = -1;
        public ArenaView Arena => _arena;
        public Camera Camera => _app.MainCamera;

        public void Init(GameApp app, GameClient client, MatchInfo match)
        {
            _app = app;
            _client = client;

            _arena = new GameObject("Arena").AddComponent<ArenaView>();
            _arena.transform.SetParent(transform, false);
            _arena.Build(match.Layout, client.Tiles, match.MatchSeed);

            for (int i = 0; i < GameWorld.MaxPlayers; i++) _avatars[i] = CreateAvatar(i);
            BuildViewmodel();
            BuildSparks();

            RenderSettings.skybox = _app.SkyMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.32f, 0.4f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.08f, 0.13f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;

            client.OnShatter += HandleShatter;
            client.OnWorldEvent += HandleWorldEvent;
            client.OnLocalAction += HandleLocalAction;
            client.OnPhaseChanged += HandlePhase;
        }

        private void OnDestroy()
        {
            if (_client == null) return;
            _client.OnShatter -= HandleShatter;
            _client.OnWorldEvent -= HandleWorldEvent;
            _client.OnLocalAction -= HandleLocalAction;
            _client.OnPhaseChanged -= HandlePhase;
            _app.Fracture.ClearAll();
            if (_viewmodel != null) Destroy(_viewmodel.gameObject);
            RenderSettings.fog = false;
        }

        // ───────────────────────────── construction ─────────────────────────────

        private Avatar CreateAvatar(int slot)
        {
            Color c = Cosmetics.SlotColor(slot);
            var a = new Avatar { Root = new GameObject("Player_" + slot) };
            a.Root.transform.SetParent(transform, false);

            var body = Part(a.Root.transform, Meshes.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), Mats.Lit(c * 0.3f, c * 0.6f));
            var visor = Part(a.Root.transform, Meshes.Cube, new Vector3(0f, 1.55f, 0.33f), new Vector3(0.55f, 0.14f, 0.12f), Mats.Neon(c, 2.5f));
            var bootL = Part(a.Root.transform, Meshes.Cylinder, new Vector3(-0.18f, 0.05f, 0f), new Vector3(0.24f, 0.05f, 0.3f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f), c * 0.5f));
            var bootR = Part(a.Root.transform, Meshes.Cylinder, new Vector3(0.18f, 0.05f, 0f), new Vector3(0.24f, 0.05f, 0.3f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f), c * 0.5f));

            a.WeaponPivot = new GameObject("WeaponPivot").transform;
            a.WeaponPivot.SetParent(a.Root.transform, false);
            a.WeaponPivot.localPosition = new Vector3(0.35f, 1.3f, 0.15f);
            a.Weapon = Part(a.WeaponPivot, Meshes.Cube, new Vector3(0f, 0f, 0.35f), new Vector3(0.14f, 0.14f, 0.7f), Mats.Lit(Color.gray, Color.cyan));

            a.AnchorRing = new GameObject("AnchorRing");
            a.AnchorRing.transform.SetParent(a.Root.transform, false);
            a.AnchorRing.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            a.AnchorRing.AddComponent<MeshFilter>().sharedMesh = Meshes.Ring(0.6f, 0.1f);
            a.AnchorRing.AddComponent<MeshRenderer>().sharedMaterial = Mats.Neon(new Color(0f, 1f, 1f, 0.9f), 3f);
            a.AnchorRing.SetActive(false);

            var trailGo = new GameObject("BootTrail");
            trailGo.transform.SetParent(a.Root.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            a.Trail = trailGo.AddComponent<TrailRenderer>();
            a.Trail.time = 0.45f;
            a.Trail.widthMultiplier = 0.28f;
            a.Trail.minVertexDistance = 0.1f;
            a.Trail.sharedMaterial = Mats.Neon(Color.white, 2f);
            a.Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            a.Trail.emitting = false;

            a.BodyRenderers = new Renderer[] { body, visor, bootL, bootR, a.Weapon };
            a.Root.SetActive(false);
            return a;
        }

        private static MeshRenderer Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }

        private void BuildViewmodel()
        {
            _viewmodel = new GameObject("Viewmodel").transform;
            _viewmodel.SetParent(_app.MainCamera.transform, false);
            _viewmodel.localPosition = new Vector3(0.32f, -0.28f, 0.55f);
            _viewmodelRenderer = Part(_viewmodel, Meshes.Cube, new Vector3(0f, 0f, 0.2f), new Vector3(0.12f, 0.12f, 0.55f), Mats.Lit(Color.gray, Color.cyan));
            Part(_viewmodel, Meshes.Cube, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.05f, 0.04f, 0.2f), Mats.Neon(Color.cyan, 3f));
        }

        private void BuildSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            _sparks = go.AddComponent<ParticleSystem>();
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _sparks.emission;
            emission.rateOverTime = 0f;
            var shape = _sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mats.Neon(Color.white, 3f);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.mesh = null;
            _sparks.Play();
        }

        private void EmitSparks(Vector3 pos, Color color, int count)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, startColor = color, applyShapeToPosition = true };
            _sparks.Emit(ep, count);
        }

        // ───────────────────────────── events ─────────────────────────────

        private void HandleShatter(ShatterEvent e)
        {
            _app.Fracture.Shatter(_arena, e.TileId, e.ImpactPoint, e.Force);
            float dist = (e.ImpactPoint.ToUnity() - _app.MainCamera.transform.position).magnitude;
            _trauma = Mathf.Min(1f, _trauma + Mathf.Clamp01(1f - dist / 25f) * 0.35f * (0.4f + e.Force));
        }

        private void HandleWorldEvent(WorldEvent e)
        {
            Vector3 p = e.Point.ToUnity();
            switch (e.Type)
            {
                case WorldEventType.ProjectileImpact:
                    AudioService.Instance?.PlayAt(AudioService.Impact, p, 0.8f, e.B == WeaponCatalog.Findling.Id ? 0.7f : 1.1f);
                    EmitSparks(p, new Color(0.7f, 1f, 1f), e.B == WeaponCatalog.Findling.Id ? 60 : 16);
                    break;

                case WorldEventType.PlayerHit:
                    if (e.B == _client.LocalSlot && e.A != _client.LocalSlot)
                    {
                        HitMarkerUntil = Time.time + 0.18f;
                        AudioService.Instance?.Play(AudioService.Hit, 0.8f);
                    }
                    if (e.A == _client.LocalSlot)
                    {
                        DamageFlashUntil = Time.time + 0.25f;
                        _trauma = Mathf.Min(1f, _trauma + 0.3f);
                    }
                    break;

                case WorldEventType.Knockout:
                    string victim = NameOf(e.A);
                    var cause = (KnockoutCause)e.C;
                    string how = cause == KnockoutCause.Fall ? "fell into the void" : (cause == KnockoutCause.Shards ? "was shredded by shards" : "was shattered");
                    string text = e.B >= 0 ? $"{NameOf(e.B)} ▸ {victim} {how}" : $"{victim} {how} (self)";
                    AddFeed(text, e.B >= 0 ? Cosmetics.SlotColor(e.B) : new Color(1f, 0.4f, 0.4f));
                    if (e.A == _client.LocalSlot)
                    {
                        LocalRespawnAt = Time.time + MatchTimings.Respawn;
                        LastKillerSlot = e.B;
                        AudioService.Instance?.Play(AudioService.Knockout, 1f, 0.8f);
                    }
                    else if (e.B == _client.LocalSlot) AudioService.Instance?.Play(AudioService.Knockout, 1f, 1.2f);
                    break;

                case WorldEventType.Respawn:
                    EmitSparks(p + Vector3.up, Cosmetics.SlotColor(e.A), 40);
                    if (e.A == _client.LocalSlot)
                    {
                        // Adopt the server's spawn facing (towards the arena centre).
                        _app.Input.Yaw = e.Value;
                        _app.Input.Pitch = 0f;
                        LastKillerSlot = -1;
                    }
                    break;

                case WorldEventType.AnchorStarted:
                    if (e.A != _client.LocalSlot)
                        AudioService.Instance?.PlayAt(AudioService.Anchor, _client.RenderPlayers[e.A].Position.ToUnity(), 0.8f);
                    break;

                case WorldEventType.PressureWave:
                    ShowBanner("PRESSURE WAVE — standard glass weakened", new Color(1f, 0.6f, 0.2f), 3f);
                    AudioService.Instance?.Play(AudioService.Wave, 1f);
                    _trauma = 0.8f;
                    break;

                case WorldEventType.CascadeWarning:
                    ShowBanner("FRACTURE CASCADE IN 30 SECONDS", new Color(1f, 0.25f, 0.3f), 4f);
                    AudioService.Instance?.Play(AudioService.Siren, 0.8f);
                    break;

                case WorldEventType.ErosionWave:
                    _arena.OnErosionWave(e.B);
                    if (e.A > 0) ShowBanner($"EDGE COLLAPSE {e.B + 1}/{MatchTimings.ErosionWaveCount}", new Color(1f, 0.3f, 0.2f), 2f);
                    break;
            }
        }

        private void HandleLocalAction(PlayerStepEvents ev)
        {
            Vector3 pos = _client.LocalPlayer.Position.ToUnity();
            if (ev.Fired)
            {
                WeaponSpec w = WeaponCatalog.Get(ev.FiredWeapon);
                string sfx = w.Id == 0 ? AudioService.FireLight : w.Id == 1 ? AudioService.FireHeavy : w.Id == 2 ? AudioService.FireBoulder : AudioService.FireScatter;
                AudioService.Instance?.Play(sfx, 0.9f);
                _viewKick = Mathf.Min(1f, _viewKick + w.RecoilDeltaV / 6f);
                _fovKick = Mathf.Min(8f, _fovKick + w.RecoilDeltaV * 0.6f);
                _trauma = Mathf.Min(1f, _trauma + w.RecoilDeltaV / 30f);
                for (int i = 0; i < w.Pellets; i++)
                {
                    Vec3 dir = ev.AimDirection;
                    if (w.SpreadDegrees > 0f)
                    {
                        var rng = new DeterministicRandom((ulong)Random.Range(1, int.MaxValue));
                        dir = GameWorld.ApplySpread(dir, w.SpreadDegrees, ref rng);
                    }
                    SpawnLocalProjectile(ev.MuzzleOrigin, dir * w.ProjectileSpeed, w.Id);
                }
            }
            if (ev.Jumped) AudioService.Instance?.PlayAt(AudioService.Jump, pos, 0.6f);
            if (ev.AnchorStarted) AudioService.Instance?.Play(AudioService.Anchor, 1f);
            if (ev.AnchorBroken) AudioService.Instance?.Play(AudioService.CrackSmall, 0.8f);
            if (ev.WeaponSwitched) AudioService.Instance?.Play(AudioService.TinkHigh, 0.4f, 0.8f);
        }

        private void HandlePhase(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Countdown: ShowBanner("GET READY", Color.cyan, 2f); break;
                case MatchPhase.Live: ShowBanner("SHATTER!", Color.cyan, 1.5f); AudioService.Instance?.Play(AudioService.Go); break;
                case MatchPhase.Cascade: ShowBanner("DYNAMIC FRACTURE CASCADE", new Color(1f, 0.3f, 0.3f), 3f); AudioService.Instance?.Play(AudioService.Siren); break;
                case MatchPhase.Overtime: ShowBanner("OVERTIME — NEXT KNOCKOUT WINS", new Color(1f, 0.8f, 0.2f), 3f); break;
                case MatchPhase.MatchEnd: ShowBanner(WinnerText(), Color.white, 4f); break;
            }
        }

        public void ShowBanner(string text, Color color, float seconds)
        {
            Banner = text;
            BannerColor = color;
            BannerUntil = Time.time + seconds;
        }

        private string WinnerText()
        {
            if (_client.EndReason == MatchEndReason.OvertimeExpired) return "DRAW";
            int best = -1, bestScore = int.MinValue;
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if (!_client.RenderPlayers[i].Active) continue;
                if (_client.Scores[i] > bestScore) { bestScore = _client.Scores[i]; best = i; }
            }
            if (_client.Match != null && _client.Match.Teams && best >= 0) return $"TEAM {(_client.RenderPlayers[best].Team == 0 ? "CYAN" : "MAGENTA")} WINS";
            return best >= 0 ? $"{NameOf(best).ToUpperInvariant()} WINS" : "MATCH OVER";
        }

        private void AddFeed(string text, Color color)
        {
            Feed.Add(new FeedEntry { Time = Time.time, Text = text, Color = color });
            if (Feed.Count > 6) Feed.RemoveAt(0);
        }

        public string NameOf(int slot)
        {
            var e = _client.Lobby.Find(slot);
            return e != null ? e.Name : "Player " + (slot + 1);
        }

        // ───────────────────────────── per frame ─────────────────────────────

        private void SpawnLocalProjectile(Vec3 origin, Vec3 velocity, byte weapon)
        {
            GameObject go = _localPool.Count > 0 ? _localPool.Pop() : CreateStone();
            go.SetActive(true);
            StyleStone(go, weapon, _client.LocalSlot);
            go.transform.position = origin.ToUnity();
            _localProjectiles.Add(new LocalProjectile { Go = go, Origin = origin, Velocity = velocity, Weapon = weapon });
        }

        private GameObject CreateStone()
        {
            var go = new GameObject("Stone");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.Sphere;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.35f, 0.35f, 0.38f), Color.black);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.widthMultiplier = 0.12f;
            trail.sharedMaterial = Mats.Neon(new Color(0.6f, 1f, 1f, 0.6f), 2f);
            return go;
        }

        private static void StyleStone(GameObject go, byte weapon, int owner)
        {
            float size = weapon == 0 ? 0.14f : weapon == 1 ? 0.28f : weapon == 2 ? 0.55f : 0.09f;
            go.transform.localScale = Vector3.one * size;
            var trail = go.GetComponent<TrailRenderer>();
            trail.widthMultiplier = size * 0.8f;
            Color c = Cosmetics.SlotColor(owner);
            trail.startColor = new Color(c.r, c.g, c.b, 0.7f);
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateAvatars();
            UpdateServerProjectiles();
            UpdateLocalProjectiles(dt);
        }

        private void UpdateAvatars()
        {
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                PlayerState p = _client.RenderPlayers[i];
                Avatar a = _avatars[i];
                bool local = i == _client.LocalSlot;
                bool show = p.Active && p.Alive;
                if (a.Root.activeSelf != show) a.Root.SetActive(show);
                if (!show) { a.WasAlive = false; continue; }

                Vector3 pos = p.Position.ToUnity();
                if (local) pos += _client.CorrectionOffset.ToUnity();
                if (!a.WasAlive) a.Trail.Clear();
                a.WasAlive = true;
                a.Root.transform.position = pos;
                a.Root.transform.rotation = Quaternion.Euler(0f, p.Yaw, 0f);
                a.WeaponPivot.localRotation = Quaternion.Euler(p.Pitch, 0f, 0f);
                a.AnchorRing.SetActive(p.Anchored);

                RosterEntry entry = _client.Lobby.Find(i);
                byte skin = entry != null ? entry.Skin : (byte)0;
                byte trail = entry != null ? entry.Trail : (byte)0;
                if (local)
                {
                    skin = (byte)_app.Profile.WeaponSkins[Mathf.Clamp(p.Weapon, 0, _app.Profile.WeaponSkins.Length - 1)];
                    trail = (byte)_app.Profile.Trail;
                }
                if (a.ShownSkin != skin)
                {
                    a.ShownSkin = skin;
                    var s = Cosmetics.WeaponSkins[Mathf.Clamp(skin, 0, Cosmetics.WeaponSkins.Length - 1)];
                    a.Weapon.sharedMaterial = Mats.Lit(s.Primary, s.Emission * 0.6f);
                    if (local) _viewmodelRenderer.sharedMaterial = a.Weapon.sharedMaterial;
                }
                if (a.ShownTrail != trail)
                {
                    a.ShownTrail = trail;
                    var t = Cosmetics.Trails[Mathf.Clamp(trail, 0, Cosmetics.Trails.Length - 1)];
                    a.Trail.startColor = t.Start;
                    a.Trail.endColor = t.End;
                }
                a.Trail.emitting = trail > 0 && new Vector2(p.Velocity.X, p.Velocity.Z).sqrMagnitude > 1f;

                // First person: hide the local body, keep its trail.
                bool flicker = p.SpawnProtection > 0f && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
                foreach (var r in a.BodyRenderers) r.enabled = !local && !flicker;
            }
        }

        private void UpdateServerProjectiles()
        {
            var list = _client.RenderProjectiles;
            while (_projectilePool.Count < list.Count) _projectilePool.Add(CreateStone());
            for (int i = 0; i < _projectilePool.Count; i++)
            {
                GameObject go = _projectilePool[i];
                if (i >= list.Count)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                    go.GetComponent<TrailRenderer>().Clear();
                    AudioService.Instance?.PlayAt(list[i].Weapon >= 2 ? AudioService.FireHeavy : AudioService.FireLight, list[i].Position.ToUnity(), 0.5f);
                }
                StyleStone(go, list[i].Weapon, list[i].Owner);
                go.transform.position = list[i].Position.ToUnity();
            }
        }

        private void UpdateLocalProjectiles(float dt)
        {
            for (int i = _localProjectiles.Count - 1; i >= 0; i--)
            {
                LocalProjectile lp = _localProjectiles[i];
                WeaponSpec w = WeaponCatalog.Get(lp.Weapon);
                Vec3 from = ProjectileMath.PositionAt(lp.Origin, lp.Velocity, w.GravityScale, lp.Age);
                lp.Age += dt;
                Vec3 to = ProjectileMath.PositionAt(lp.Origin, lp.Velocity, w.GravityScale, lp.Age);
                bool done = lp.Age > w.MaxLifetime;
                if (!done && _client.Tiles != null && PlayerSimulation.RaycastTiles(_client.Tiles, from, to, GameWorld.ProjectileRadius, out _, out Vec3 hit) >= 0)
                {
                    to = hit;
                    done = true;
                    EmitSparks(hit.ToUnity(), new Color(0.7f, 1f, 1f), 10);
                }
                lp.Go.transform.position = to.ToUnity();
                if (done)
                {
                    lp.Go.GetComponent<TrailRenderer>().Clear();
                    lp.Go.SetActive(false);
                    _localPool.Push(lp.Go);
                    _localProjectiles.RemoveAt(i);
                }
            }
        }

        private void LateUpdate()
        {
            Camera cam = _app.MainCamera;
            float dt = Time.deltaTime;
            PlayerState me = _client.LocalPlayer;
            MatchPhase phase = _client.Phase;
            bool firstPerson = me.Alive && (MatchClock.IsCombatPhase(phase) || phase == MatchPhase.Countdown);

            _trauma = Mathf.Max(0f, _trauma - dt * 1.5f);
            _viewKick = Mathf.Max(0f, _viewKick - dt * 5f);
            _fovKick = Mathf.Max(0f, _fovKick - dt * 20f);
            float shake = _trauma * _trauma;
            Vector3 shakeEuler = new Vector3(
                (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 6f * shake,
                (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 6f * shake,
                (Mathf.PerlinNoise(Time.time * 25f, 7f) - 0.5f) * 4f * shake);

            if (firstPerson)
            {
                Vector3 eye = (me.EyePosition + _client.CorrectionOffset).ToUnity();
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.Euler(_app.Input.Pitch - _viewKick * 4f, _app.Input.Yaw, 0f) * Quaternion.Euler(shakeEuler);
                cam.fieldOfView = _app.Settings.FieldOfView + _fovKick;
                _viewmodel.gameObject.SetActive(true);
                _viewmodel.localPosition = new Vector3(0.32f, -0.28f, 0.55f - _viewKick * 0.15f);
                _viewmodel.localRotation = Quaternion.Euler(-_viewKick * 12f, 0f, 0f);
            }
            else
            {
                _viewmodel.gameObject.SetActive(false);
                Vector3 focus = Vector3.zero;
                if (LastKillerSlot >= 0 && _client.RenderPlayers[LastKillerSlot].Alive && phase != MatchPhase.MatchEnd)
                    focus = _client.RenderPlayers[LastKillerSlot].Position.ToUnity() + Vector3.up;
                _spectateAngle += dt * 12f;
                Vector3 orbit = Quaternion.Euler(0f, _spectateAngle, 0f) * new Vector3(0f, 16f, -26f);
                cam.transform.position = Vector3.Lerp(cam.transform.position, focus + orbit, 1f - Mathf.Exp(-dt * 3f));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(focus - cam.transform.position), 1f - Mathf.Exp(-dt * 4f)) * Quaternion.Euler(shakeEuler);
                cam.fieldOfView = 60f;
            }
        }
    }
}
