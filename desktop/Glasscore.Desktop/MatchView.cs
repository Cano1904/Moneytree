using System;
using System.Collections.Generic;
using System.Numerics;
using Glasscore.Net;
using Glasscore.Simulation;
using Raylib_cs;

namespace Glasscore.Desktop
{
    /// <summary>Glass shards (Voronoi prisms) and tempered crumble cubes, simulated and drawn locally.</summary>
    public sealed class ShardSystem
    {
        private sealed class Shard
        {
            public Vector3[] Tris;       // local vertices, 3 per triangle
            public Vector3 Position, Velocity, Spin;
            public Quaternion Rotation = Quaternion.Identity;
            public float Life, MaxLife;
            public Color Color;
            public bool Cube;
            public float Size;
        }

        private readonly List<Shard> _shards = new List<Shard>();
        private readonly List<int> _scratch = new List<int>(16);
        private List<FractureCell>[] _prebaked;

        public int Count => _shards.Count;

        public void Init()
        {
            _prebaked = new List<FractureCell>[4];
            for (int p = 0; p < 4; p++)
                _prebaked[p] = VoronoiFracture2D.ComputeCells(VoronoiFracture2D.GenerateSites((ulong)(p + 1) * 7919UL, Vec2.Zero, 1f, 1f, 12, 0.5f), 1f, 1f);
        }

        public static Vector3[] Triangles(FractureCell cell)
        {
            MeshData m = PrismMeshBuilder.Build(cell.Polygon, GlassCatalog.TileThickness, cell.Centroid, 1f);
            var tris = new Vector3[m.Triangles.Length];
            for (int i = 0; i < tris.Length; i++)
            {
                Vec3 v = m.Vertices[m.Triangles[i]];
                tris[i] = new Vector3(v.X, v.Y, v.Z);
            }
            return tris;
        }

        /// <summary>RPC_ShatterTile on the client: same seed → same pieces on every machine.</summary>
        public void Shatter(TileLayout layout, int matchSeed, int tileId, Vec3 impact, float force, (int sites, bool prebaked, int maxShards, int crumble) quality)
        {
            GlassType type = layout.Types[tileId];
            GlassSpec spec = GlassCatalog.Get(type);
            Vec3 c3 = layout.Centers[tileId];
            var center = new Vector3(c3.X, c3.Y, c3.Z);
            Color tint = Palette.GlassTint(type, 2.2f);

            if (spec.BreakStyle == BreakStyle.CubeCrumble)
            {
                int g = quality.crumble;
                float cell = GlassCatalog.TileSize / g;
                var rnd = new Random(tileId);
                for (int x = 0; x < g; x++)
                    for (int z = 0; z < g; z++)
                    {
                        _shards.Add(new Shard
                        {
                            Cube = true,
                            Size = cell * 0.8f,
                            Position = center + new Vector3((x + 0.5f) * cell - 1f, 0f, (z + 0.5f) * cell - 1f),
                            Velocity = new Vector3((float)rnd.NextDouble() * 2 - 1, ((float)rnd.NextDouble() * 2 - 0.5f) * (0.3f + force), (float)rnd.NextDouble() * 2 - 1),
                            MaxLife = 1.6f + (float)rnd.NextDouble(),
                            Color = new Color((int)tint.R, (int)tint.G, (int)tint.B, 170),
                        });
                    }
                Sfx.Play(Sfx.Crumble, 0.9f);
                Trim(quality.maxShards * 2);
                return;
            }

            ulong seed = DeterministicHash.FractureSeed(matchSeed, tileId, impact);
            Vec2 local = (impact - c3).XZ;
            List<FractureCell> cells;
            float rotation = 0f;
            if (quality.prebaked)
            {
                cells = _prebaked[(int)(seed % 4UL)];
                rotation = (seed >> 8) % 4UL * MathF.PI / 2f;
            }
            else
            {
                cells = VoronoiFracture2D.ComputeCells(VoronoiFracture2D.GenerateSites(seed, local, 1f, 1f, quality.sites, force), 1f, 1f);
            }

            var rng = new DeterministicRandom(seed ^ 0x5DEECE66DUL);
            Quaternion rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation);
            var impactLocal = new Vector3(local.X, 0f, local.Y);
            foreach (FractureCell cellInfo in cells)
            {
                Vector3 offset = Vector3.Transform(new Vector3(cellInfo.Centroid.X, 0f, cellInfo.Centroid.Y), rot);
                Vector3 outward = offset - impactLocal;
                outward.Y = 0f;
                outward = outward.LengthSquared() > 1e-4f ? Vector3.Normalize(outward) : Vector3.UnitX;
                float speed = (1.2f + 6f * force) * (0.5f + rng.NextFloat()) / (0.6f + (offset - impactLocal).Length());
                _shards.Add(new Shard
                {
                    Tris = Triangles(cellInfo),
                    Position = center + offset,
                    Rotation = rot,
                    Velocity = outward * speed + new Vector3(0f, rng.Range(-0.5f, 2.5f) * force - 1.5f, 0f),
                    Spin = new Vector3(rng.Range(-8f, 8f), rng.Range(-8f, 8f), rng.Range(-8f, 8f)) * force,
                    MaxLife = 3.5f * rng.Range(0.7f, 1.3f),
                    Color = tint,
                });
            }
            Trim(quality.maxShards);
        }

        private void Trim(int max)
        {
            if (_shards.Count > max) _shards.RemoveRange(0, _shards.Count - max);
        }

        public void Update(float dt, TileLayout layout, TileGridModel tiles)
        {
            float half = GlassCatalog.TileSize * 0.5f, halfT = GlassCatalog.TileThickness * 0.5f;
            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                Shard s = _shards[i];
                s.Life += dt;
                if (s.Life >= s.MaxLife || s.Position.Y < -PlayerRules.KillPlaneBelowLowestLayer) { _shards.RemoveAt(i); continue; }
                s.Velocity.Y -= PlayerRules.Gravity * (s.Cube ? 0.6f : 1f) * dt;
                Vector3 next = s.Position + s.Velocity * dt;

                // Land on intact glass.
                if (layout != null && tiles != null && s.Velocity.Y < 0f)
                {
                    _scratch.Clear();
                    PlayerSimulation.QueryTiles(layout, new Vec3(next.X, next.Y - 0.2f, next.Z), new Vec3(next.X, s.Position.Y + 0.1f, next.Z), _scratch);
                    foreach (int id in _scratch)
                    {
                        if (!tiles.IsSolid(id)) continue;
                        Vec3 c = layout.Centers[id];
                        float top = c.Y + halfT + 0.03f;
                        if (Math.Abs(next.X - c.X) > half || Math.Abs(next.Z - c.Z) > half) continue;
                        if (s.Position.Y >= top && next.Y < top)
                        {
                            next.Y = top;
                            s.Velocity = new Vector3(s.Velocity.X * 0.5f, -s.Velocity.Y * 0.2f, s.Velocity.Z * 0.5f);
                            s.Spin *= 0.5f;
                        }
                    }
                }
                s.Position = next;
                float angle = s.Spin.Length() * dt;
                if (angle > 1e-5f) s.Rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(s.Spin), angle) * s.Rotation);
            }
        }

        public void Draw()
        {
            foreach (Shard s in _shards)
            {
                float fade = Math.Clamp((s.MaxLife - s.Life) / 0.5f, 0f, 1f);
                Color c = new Color(s.Color.R, s.Color.G, s.Color.B, (int)(s.Color.A * fade));
                if (s.Cube)
                {
                    Raylib.DrawCube(s.Position, s.Size, s.Size, s.Size, c);
                    continue;
                }
                Color edge = new Color(200, 255, 255, (int)(160 * fade));
                Matrix4x4 m = Matrix4x4.CreateFromQuaternion(s.Rotation);
                for (int t = 0; t + 2 < s.Tris.Length; t += 3)
                {
                    Vector3 a = Vector3.Transform(s.Tris[t], m) + s.Position;
                    Vector3 b = Vector3.Transform(s.Tris[t + 1], m) + s.Position;
                    Vector3 d = Vector3.Transform(s.Tris[t + 2], m) + s.Position;
                    Raylib.DrawTriangle3D(a, b, d, c);
                    if (t < 3) Raylib.DrawLine3D(a, b, edge);
                }
            }
        }

        public void Clear() => _shards.Clear();
    }

    /// <summary>Everything drawn in 3D during a match plus the HUD event feed.</summary>
    public sealed class MatchView
    {
        public struct FeedEntry { public double Time; public string Text; public Color Color; }
        private struct Spark { public Vector3 P, V; public float Life; public Color C; }
        private struct LocalProjectile { public Vec3 Origin, Velocity; public float Age; public byte Weapon; }

        private readonly GameClient _client;
        private readonly App _app;
        private readonly TileLayout _layout;
        private readonly int _seed;
        private readonly Vector3[][] _cracks;          // per tile: crack segments (pairs) on the top face
        private readonly bool[] _destabilized;
        private readonly float _maxRadius;
        private readonly List<int> _waveScratch = new List<int>();
        private readonly List<int> _order = new List<int>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<LocalProjectile> _local = new List<LocalProjectile>();
        private readonly List<Vector3>[] _trails = new List<Vector3>[GameWorld.MaxPlayers];
        private readonly float[] _shownIntegrity;
        private readonly (Vector3 pos, Vector3 size, float yaw)[] _monoliths;
        public readonly ShardSystem Shards = new ShardSystem();
        public readonly List<FeedEntry> Feed = new List<FeedEntry>();

        public string Banner;
        public double BannerUntil;
        public Color BannerColor = Color.White;
        public double HitMarkerUntil, DamageFlashUntil, LocalRespawnAt;
        public int LastKiller = -1;
        public float Trauma;
        public float ViewKick;
        public Camera3D Camera;
        private float _spectate;

        public MatchView(App app, GameClient client, MatchInfo match)
        {
            _app = app;
            _client = client;
            _layout = match.Layout;
            _seed = match.MatchSeed;
            Shards.Init();
            _cracks = new Vector3[_layout.Count][];
            _shownIntegrity = new float[_layout.Count];
            _destabilized = new bool[_layout.Count];
            _maxRadius = ErosionPlanner.MaxRadius(_layout);
            for (int i = 0; i < _layout.Count; i++)
            {
                _cracks[i] = BuildCracks(i);
                _shownIntegrity[i] = _layout.MaxIntegrity(i);
            }
            for (int i = 0; i < _trails.Length; i++) _trails[i] = new List<Vector3>();
            var rng = new DeterministicRandom((ulong)(_seed + 99));
            _monoliths = new (Vector3, Vector3, float)[24];
            for (int i = 0; i < _monoliths.Length; i++)
            {
                float a = rng.NextFloat() * MathF.PI * 2f, r = _maxRadius + 18f + rng.NextFloat() * 40f;
                _monoliths[i] = (new Vector3(MathF.Cos(a) * r, rng.Range(-15f, 25f), MathF.Sin(a) * r), new Vector3(rng.Range(2f, 6f), rng.Range(8f, 30f), rng.Range(2f, 6f)), a);
            }
            Camera = new Camera3D { Position = new Vector3(0, 20, -30), Target = Vector3.Zero, Up = Vector3.UnitY, FovY = 70f, Projection = CameraProjection.Perspective };

            client.OnShatter += OnShatter;
            client.OnWorldEvent += OnWorldEvent;
            client.OnLocalAction += OnLocalAction;
            client.OnPhaseChanged += OnPhase;
        }

        public void Detach()
        {
            _client.OnShatter -= OnShatter;
            _client.OnWorldEvent -= OnWorldEvent;
            _client.OnLocalAction -= OnLocalAction;
            _client.OnPhaseChanged -= OnPhase;
        }

        private Vector3[] BuildCracks(int tile)
        {
            // Deterministic jagged cracks radiating from a point on the pane (drawn progressively).
            var rng = new DeterministicRandom((ulong)(tile * 7349 + 11));
            var segs = new List<Vector3>();
            var origin = new Vector3(rng.Range(-0.5f, 0.5f), 0f, rng.Range(-0.5f, 0.5f));
            for (int k = 0; k < 7; k++)
            {
                float ang = k / 7f * MathF.PI * 2f + rng.Range(-0.3f, 0.3f);
                Vector3 p = origin;
                for (int step = 0; step < 4; step++)
                {
                    ang += rng.Range(-0.5f, 0.5f);
                    var q = p + new Vector3(MathF.Cos(ang), 0f, MathF.Sin(ang)) * rng.Range(0.15f, 0.35f);
                    q.X = Math.Clamp(q.X, -1f, 1f);
                    q.Z = Math.Clamp(q.Z, -1f, 1f);
                    segs.Add(p);
                    segs.Add(q);
                    p = q;
                }
            }
            return segs.ToArray();
        }

        public static Vector3 V(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        // ───────────────────────────── events ─────────────────────────────

        private void OnShatter(ShatterEvent e)
        {
            Shards.Shatter(_layout, _seed, e.TileId, e.ImpactPoint, e.Force, _app.Config.Fracture);
            if (_layout.Types[e.TileId] != GlassType.Tempered)
                PlayAt(_app.Config.FractureQuality > 0 ? Sfx.Shatter : Sfx.ShatterSmall, V(e.ImpactPoint), 0.7f + 0.5f * e.Force);
            float dist = Vector3.Distance(V(e.ImpactPoint), Camera.Position);
            Trauma = Math.Min(1f, Trauma + Math.Clamp(1f - dist / 25f, 0f, 1f) * 0.35f * (0.4f + e.Force));
        }

        private void OnWorldEvent(WorldEvent e)
        {
            Vector3 p = V(e.Point);
            switch (e.Type)
            {
                case WorldEventType.ProjectileImpact:
                    PlayAt(Sfx.Impact, p, 0.8f, e.B == WeaponCatalog.Findling.Id ? 0.7f : 1.1f);
                    Emit(p, new Color(180, 255, 255, 255), e.B == WeaponCatalog.Findling.Id ? 40 : 12);
                    break;
                case WorldEventType.PlayerHit:
                    if (e.B == _client.LocalSlot && e.A != _client.LocalSlot) { HitMarkerUntil = Raylib.GetTime() + 0.18; Sfx.Play(Sfx.Hit, 0.8f); }
                    if (e.A == _client.LocalSlot) { DamageFlashUntil = Raylib.GetTime() + 0.25; Trauma = Math.Min(1f, Trauma + 0.3f); }
                    break;
                case WorldEventType.Knockout:
                    var cause = (KnockoutCause)e.C;
                    string how = cause == KnockoutCause.Fall ? "fell into the void" : cause == KnockoutCause.Shards ? "was shredded by shards" : "was shattered";
                    AddFeed(e.B >= 0 ? $"{_app.NameOf(e.B)} > {_app.NameOf(e.A)} {how}" : $"{_app.NameOf(e.A)} {how} (self)", e.B >= 0 ? Palette.Slot(e.B) : Palette.Red);
                    if (e.A == _client.LocalSlot) { LocalRespawnAt = Raylib.GetTime() + MatchTimings.Respawn; LastKiller = e.B; Sfx.Play(Sfx.Knockout, 1f, 0.8f); }
                    else if (e.B == _client.LocalSlot) Sfx.Play(Sfx.Knockout, 1f, 1.2f);
                    break;
                case WorldEventType.Respawn:
                    Emit(p + Vector3.UnitY, Palette.Slot(e.A), 30);
                    _trails[e.A].Clear();
                    if (e.A == _client.LocalSlot) { _app.Look.Yaw = e.Value; _app.Look.Pitch = 0f; LastKiller = -1; }
                    break;
                case WorldEventType.AnchorStarted:
                    if (e.A != _client.LocalSlot) PlayAt(Sfx.Anchor, V(_client.RenderPlayers[e.A].Position), 0.8f);
                    break;
                case WorldEventType.PressureWave:
                    ShowBanner("PRESSURE WAVE - standard glass weakened", new Color(255, 153, 51, 255), 3);
                    Sfx.Play(Sfx.Wave);
                    Trauma = 0.8f;
                    break;
                case WorldEventType.CascadeWarning:
                    ShowBanner("FRACTURE CASCADE IN 30 SECONDS", Palette.Red, 4);
                    Sfx.Play(Sfx.Siren, 0.8f);
                    break;
                case WorldEventType.ErosionWave:
                    ErosionPlanner.CollectWave(_layout, _client.Tiles, _destabilized, _maxRadius, e.B, _waveScratch);
                    if (e.A > 0) ShowBanner($"EDGE COLLAPSE {e.B + 1}/{MatchTimings.ErosionWaveCount}", Palette.Red, 2);
                    break;
            }
        }

        private void OnLocalAction(PlayerStepEvents ev)
        {
            if (ev.Fired)
            {
                WeaponSpec w = WeaponCatalog.Get(ev.FiredWeapon);
                Sfx.Play(w.Id == 0 ? Sfx.FireLight : w.Id == 1 ? Sfx.FireHeavy : w.Id == 2 ? Sfx.FireBoulder : Sfx.FireScatter, 0.9f);
                ViewKick = Math.Min(1f, ViewKick + w.RecoilDeltaV / 6f);
                Trauma = Math.Min(1f, Trauma + w.RecoilDeltaV / 30f);
                var rng = new DeterministicRandom((ulong)Random.Shared.NextInt64());
                for (int i = 0; i < w.Pellets; i++)
                {
                    Vec3 dir = w.SpreadDegrees > 0f ? GameWorld.ApplySpread(ev.AimDirection, w.SpreadDegrees, ref rng) : ev.AimDirection;
                    _local.Add(new LocalProjectile { Origin = ev.MuzzleOrigin, Velocity = dir * w.ProjectileSpeed, Weapon = w.Id });
                }
            }
            if (ev.Jumped) Sfx.Play(Sfx.Jump, 0.6f);
            if (ev.AnchorStarted) Sfx.Play(Sfx.Anchor);
            if (ev.AnchorBroken) Sfx.Play(Sfx.CrackSmall, 0.8f);
            if (ev.WeaponSwitched) Sfx.Play(Sfx.TinkHigh, 0.4f, 0.8f);
        }

        private void OnPhase(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Countdown: ShowBanner("GET READY", Palette.Cyan, 2); break;
                case MatchPhase.Live: ShowBanner("SHATTER!", Palette.Cyan, 1.5); Sfx.Play(Sfx.Go); break;
                case MatchPhase.Cascade: ShowBanner("DYNAMIC FRACTURE CASCADE", Palette.Red, 3); Sfx.Play(Sfx.Siren); break;
                case MatchPhase.Overtime: ShowBanner("OVERTIME - NEXT KNOCKOUT WINS", Palette.Gold, 3); break;
                case MatchPhase.MatchEnd: ShowBanner(WinnerText(), Color.White, 4); break;
            }
        }

        private string WinnerText()
        {
            if (_client.EndReason == MatchEndReason.OvertimeExpired) return "DRAW";
            int best = -1, bestScore = int.MinValue;
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
                if (_client.RenderPlayers[i].Active && _client.Scores[i] > bestScore) { bestScore = _client.Scores[i]; best = i; }
            if (_client.Match != null && _client.Match.Teams && best >= 0) return _client.RenderPlayers[best].Team == 0 ? "TEAM CYAN WINS" : "TEAM MAGENTA WINS";
            return best >= 0 ? _app.NameOf(best).ToUpperInvariant() + " WINS" : "MATCH OVER";
        }

        public void ShowBanner(string text, Color c, double seconds) { Banner = text; BannerColor = c; BannerUntil = Raylib.GetTime() + seconds; }

        private void AddFeed(string text, Color c)
        {
            Feed.Add(new FeedEntry { Time = Raylib.GetTime(), Text = text, Color = c });
            if (Feed.Count > 6) Feed.RemoveAt(0);
        }

        private void Emit(Vector3 p, Color c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3((float)Random.Shared.NextDouble() * 2 - 1, (float)Random.Shared.NextDouble() * 1.5f, (float)Random.Shared.NextDouble() * 2 - 1);
                _sparks.Add(new Spark { P = p, V = v * (2f + (float)Random.Shared.NextDouble() * 6f), Life = 0.3f + (float)Random.Shared.NextDouble() * 0.4f, C = c });
            }
            if (_sparks.Count > 1500) _sparks.RemoveRange(0, _sparks.Count - 1500);
        }

        /// <summary>Distance attenuation + stereo pan relative to the camera.</summary>
        public void PlayAt(string id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            Vector3 to = pos - Camera.Position;
            float dist = to.Length();
            float gain = volume * Math.Clamp(1f - (dist - 3f) / 67f, 0f, 1f);
            if (gain <= 0.01f) return;
            Vector3 fwd = Vector3.Normalize(Camera.Target - Camera.Position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            float side = dist > 0.01f ? Vector3.Dot(Vector3.Normalize(to), right) : 0f;
            Sfx.Play(id, gain, pitch * (0.94f + (float)Random.Shared.NextDouble() * 0.12f), 0.5f - side * 0.4f);
        }

        // ───────────────────────────── update ─────────────────────────────

        public void Update(float dt)
        {
            Shards.Update(dt, _layout, _client.Tiles);
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                Spark s = _sparks[i];
                s.Life -= dt;
                if (s.Life <= 0f) { _sparks.RemoveAt(i); continue; }
                s.V.Y -= 20f * dt;
                s.P += s.V * dt;
                _sparks[i] = s;
            }
            for (int i = _local.Count - 1; i >= 0; i--)
            {
                LocalProjectile lp = _local[i];
                WeaponSpec w = WeaponCatalog.Get(lp.Weapon);
                Vec3 from = ProjectileMath.PositionAt(lp.Origin, lp.Velocity, w.GravityScale, lp.Age);
                lp.Age += dt;
                Vec3 to = ProjectileMath.PositionAt(lp.Origin, lp.Velocity, w.GravityScale, lp.Age);
                bool done = lp.Age > w.MaxLifetime;
                if (!done && _client.Tiles != null && PlayerSimulation.RaycastTiles(_client.Tiles, from, to, GameWorld.ProjectileRadius, out _, out Vec3 hit) >= 0)
                {
                    done = true;
                    Emit(V(hit), new Color(180, 255, 255, 255), 8);
                }
                if (done) _local.RemoveAt(i); else _local[i] = lp;
            }

            // Tile damage creaks.
            if (_client.Tiles != null)
                for (int i = 0; i < _layout.Count; i++)
                {
                    float integ = _client.Tiles.GetIntegrity(i);
                    if (integ < _shownIntegrity[i] && integ > 0f && Random.Shared.NextDouble() < 0.5)
                        PlayAt(Sfx.CrackSmall, V(_layout.Centers[i]), 0.5f, 0.9f + 0.3f * integ / _layout.MaxIntegrity(i));
                    _shownIntegrity[i] = integ;
                }

            // Boot trails.
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                PlayerState p = _client.RenderPlayers[i];
                var trail = _trails[i];
                if (!p.Alive) { trail.Clear(); continue; }
                Vector3 pos = V(p.Position) + new Vector3(0, 0.08f, 0);
                if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], pos) > 0.15f) trail.Add(pos);
                if (trail.Count > 24 || (trail.Count > 0 && new Vector2(p.Velocity.X, p.Velocity.Z).LengthSquared() < 1f)) trail.RemoveAt(0);
            }

            Trauma = Math.Max(0f, Trauma - dt * 1.5f);
            ViewKick = Math.Max(0f, ViewKick - dt * 5f);
            UpdateCamera(dt);
        }

        private void UpdateCamera(float dt)
        {
            PlayerState me = _client.LocalPlayer;
            MatchPhase phase = _client.Phase;
            float shake = Trauma * Trauma;
            float t = (float)Raylib.GetTime();
            float sx = (Noise(t * 25f) - 0.5f) * 6f * shake, sy = (Noise(t * 25f + 50f) - 0.5f) * 6f * shake;
            bool firstPerson = me.Alive && (MatchClock.IsCombatPhase(phase) || phase == MatchPhase.Countdown);
            if (firstPerson)
            {
                Vector3 eye = V(me.EyePosition + _client.CorrectionOffset);
                Vector3 dir = V(PlayerSimulation.LookDirection(_app.Look.Yaw + sx, Math.Clamp(_app.Look.Pitch - ViewKick * 4f + sy, -89f, 89f)));
                Camera.Position = eye;
                Camera.Target = eye + dir;
                Camera.FovY = VerticalFov(_app.Config.FieldOfView);
            }
            else
            {
                Vector3 focus = Vector3.Zero;
                if (LastKiller >= 0 && _client.RenderPlayers[LastKiller].Alive && phase != MatchPhase.MatchEnd)
                    focus = V(_client.RenderPlayers[LastKiller].Position) + Vector3.UnitY;
                _spectate += dt * 0.2f;
                Vector3 want = focus + new Vector3(MathF.Sin(_spectate) * 26f, 16f, -MathF.Cos(_spectate) * 26f);
                Camera.Position = Vector3.Lerp(Camera.Position, want, 1f - MathF.Exp(-dt * 3f));
                Camera.Target = Vector3.Lerp(Camera.Target, focus, 1f - MathF.Exp(-dt * 4f)) + new Vector3(sx, sy, 0) * 0.05f;
                Camera.FovY = 55f;
            }
        }

        private static float VerticalFov(float horizontalDeg)
        {
            float aspect = Raylib.GetScreenWidth() / (float)Math.Max(1, Raylib.GetScreenHeight());
            return 2f * MathF.Atan(MathF.Tan(horizontalDeg * MathF.PI / 360f) / aspect) * 180f / MathF.PI;
        }

        private static float Noise(float x) => 0.5f + 0.25f * MathF.Sin(x * 1.7f) + 0.25f * MathF.Sin(x * 3.1f + 1.3f);

        // ───────────────────────────── draw ─────────────────────────────

        public void Draw3D()
        {
            float time = (float)Raylib.GetTime();
            // Scenery.
            foreach (var m in _monoliths) Raylib.DrawCubeWires(m.pos, m.size.X, m.size.Y, m.size.Z, new Color(0, 255, 255, 40));
            float voidY = -PlayerRules.KillPlaneBelowLowestLayer - 2f;
            for (int k = 1; k <= 5; k++) Raylib.DrawCircle3D(new Vector3(0, voidY, 0), (_maxRadius + 18f) * k / 5f, Vector3.UnitX, 90f, new Color(255, 51, 102, 70));

            // Opaque: avatars, projectiles, sparks.
            for (int i = 0; i < GameWorld.MaxPlayers; i++) DrawAvatar(i, time);
            foreach (var p in _client.RenderProjectiles) DrawStone(V(p.Position), V(p.Velocity), p.Weapon, p.Owner);
            foreach (var lp in _local)
            {
                WeaponSpec w = WeaponCatalog.Get(lp.Weapon);
                Vec3 pos = ProjectileMath.PositionAt(lp.Origin, lp.Velocity, w.GravityScale, lp.Age);
                DrawStone(V(pos), V(lp.Velocity), lp.Weapon, _client.LocalSlot);
            }
            foreach (var s in _sparks) Raylib.DrawCube(s.P, 0.06f, 0.06f, 0.06f, s.C);
            DrawViewmodel();

            // Transparent: glass (back to front), shards.
            Rlgl.DisableDepthMask();
            Rlgl.DisableBackfaceCulling();
            DrawTiles(time);
            Shards.Draw();
            Rlgl.EnableBackfaceCulling();
            Rlgl.EnableDepthMask();
        }

        private void DrawTiles(float time)
        {
            if (_client.Tiles == null) return;
            _order.Clear();
            for (int i = 0; i < _layout.Count; i++) if (_client.Tiles.IsSolid(i)) _order.Add(i);
            Vector3 cam = Camera.Position;
            _order.Sort((a, b) => Vector3.DistanceSquared(V(_layout.Centers[b]), cam).CompareTo(Vector3.DistanceSquared(V(_layout.Centers[a]), cam)));

            float size = GlassCatalog.TileSize, th = GlassCatalog.TileThickness;
            foreach (int i in _order)
            {
                GlassType type = _layout.Types[i];
                Vector3 c = V(_layout.Centers[i]);
                float integ = _client.Tiles.GetIntegrity(i);
                float max = _layout.MaxIntegrity(i);
                float frac = integ / max;
                Color tint = Palette.GlassTint(type);
                Color edge = Palette.GlassEdge(type);
                if (_destabilized[i])
                {
                    float pulse = 0.5f + 0.5f * MathF.Sin(time * 12f);
                    tint = new Color(255, (int)(80 + 60 * pulse), 60, (int)(80 + 80 * pulse));
                    edge = new Color(255, 80, 60, 255);
                }
                Raylib.DrawCube(c, size * 0.995f, th, size * 0.995f, tint);
                Raylib.DrawCubeWires(c, size * 0.995f, th, size * 0.995f, Palette.WithAlpha(edge, 0.35f + 0.4f * frac));

                TileVisualState state = GlassCatalog.VisualState(type, integ);
                if (state == TileVisualState.Spiderweb)
                {
                    Vector3 top = c + new Vector3(0, th * 0.5f + 0.005f, 0);
                    for (int k = 0; k < 9; k++)
                    {
                        float a = k / 9f * MathF.PI * 2f;
                        Raylib.DrawLine3D(top, top + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 0.95f, new Color(230, 255, 255, 220));
                    }
                    for (int r = 1; r <= 3; r++) Raylib.DrawCircle3D(top, r * 0.28f, Vector3.UnitX, 90f, new Color(230, 255, 255, 180));
                }
                else if (frac < 0.97f)
                {
                    var segs = _cracks[i];
                    int count = Math.Min(segs.Length / 2, (int)((1f - frac) * segs.Length / 2 * 1.3f) + 1);
                    Vector3 top = c + new Vector3(0, th * 0.5f + 0.005f, 0);
                    for (int k = 0; k < count; k++) Raylib.DrawLine3D(top + segs[k * 2], top + segs[k * 2 + 1], new Color(235, 255, 255, 230));
                }

                if (_layout.IsSpawnPad[i]) Raylib.DrawCircle3D(c + new Vector3(0, th * 0.5f + 0.01f, 0), 0.75f, Vector3.UnitX, 90f, new Color(0, 255, 255, 200));
            }
        }

        private void DrawAvatar(int slot, float time)
        {
            PlayerState p = _client.RenderPlayers[slot];
            if (!p.Active || !p.Alive) return;
            bool local = slot == _client.LocalSlot;
            Color col = Palette.Slot(slot);
            var trail = _trails[slot];
            int trailIndex = local ? _app.Config.Trail : (_app.Lobby(slot)?.Trail ?? 0);
            Color tc = Palette.Trails[Math.Clamp(trailIndex, 0, Palette.Trails.Length - 1)].Color;
            if (tc.A > 0)
                for (int k = 1; k < trail.Count; k++)
                {
                    Color c = Palette.WithAlpha(tc, k / (float)trail.Count * 0.9f);
                    Raylib.DrawLine3D(trail[k - 1], trail[k], c);
                    Raylib.DrawLine3D(trail[k - 1] + new Vector3(0.1f, 0, 0), trail[k] + new Vector3(0.1f, 0, 0), c);
                    Raylib.DrawLine3D(trail[k - 1] - new Vector3(0.1f, 0, 0), trail[k] - new Vector3(0.1f, 0, 0), c);
                }
            if (local) return; // first person: body hidden

            Vector3 feet = V(p.Position);
            bool flicker = p.SpawnProtection > 0f && (time * 8f) % 1f < 0.5f;
            if (!flicker)
            {
                Raylib.DrawCapsule(feet + new Vector3(0, 0.45f, 0), feet + new Vector3(0, 1.35f, 0), 0.4f, 10, 6, Palette.Scale(col, 0.35f));
                Raylib.DrawCapsuleWires(feet + new Vector3(0, 0.45f, 0), feet + new Vector3(0, 1.35f, 0), 0.41f, 10, 4, Palette.WithAlpha(col, 0.8f));
                Vector3 look = V(PlayerSimulation.LookDirection(p.Yaw, p.Pitch));
                Vector3 flat = Vector3.Normalize(new Vector3(look.X, 0, look.Z) + new Vector3(1e-4f, 0, 0));
                Vector3 right = Vector3.Normalize(Vector3.Cross(flat, Vector3.UnitY));
                Raylib.DrawCube(feet + new Vector3(0, 1.55f, 0) + flat * 0.33f, 0.5f, 0.14f, 0.5f, col);
                int skin = _app.Lobby(slot)?.Skin ?? 0;
                var sk = Palette.Skins[Math.Clamp(skin, 0, Palette.Skins.Length - 1)];
                Vector3 shoulder = feet + new Vector3(0, 1.3f, 0) - right * 0.38f;
                Raylib.DrawCylinderEx(shoulder, shoulder + look * 0.8f, 0.08f, 0.06f, 8, sk.Body);
                Raylib.DrawSphere(shoulder + look * 0.8f, 0.05f, sk.Glow);
            }
            if (p.Anchored) Raylib.DrawCircle3D(feet + new Vector3(0, 0.07f, 0), 0.6f, Vector3.UnitX, 90f, Palette.Cyan);
        }

        private void DrawStone(Vector3 pos, Vector3 vel, byte weapon, int owner)
        {
            float size = weapon == 0 ? 0.14f : weapon == 1 ? 0.28f : weapon == 2 ? 0.55f : 0.09f;
            Raylib.DrawSphere(pos, size, new Color(110, 110, 118, 255));
            Vector3 back = vel.LengthSquared() > 0.01f ? Vector3.Normalize(vel) : Vector3.UnitY;
            Raylib.DrawLine3D(pos, pos - back * (1.2f + size * 2f), Palette.WithAlpha(Palette.Slot(owner), 0.8f));
        }

        private void DrawViewmodel()
        {
            PlayerState me = _client.LocalPlayer;
            if (!me.Alive || !(MatchClock.IsCombatPhase(_client.Phase) || _client.Phase == MatchPhase.Countdown)) return;
            Vector3 fwd = Vector3.Normalize(Camera.Target - Camera.Position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            Vector3 up = Vector3.Cross(right, fwd);
            Vector3 basePos = Camera.Position + right * 0.22f - up * 0.2f + fwd * (0.55f - ViewKick * 0.12f);
            var sk = Palette.Skins[Math.Clamp(_app.Config.WeaponSkins[Math.Clamp((int)me.Weapon, 0, 3)], 0, Palette.Skins.Length - 1)];
            float r = me.Weapon == 2 ? 0.045f : me.Weapon == 1 ? 0.035f : 0.025f;
            Raylib.DrawCylinderEx(basePos, basePos + fwd * 0.4f + up * ViewKick * 0.06f, r, r * 0.8f, 10, sk.Body);
            Raylib.DrawCylinderWiresEx(basePos, basePos + fwd * 0.4f + up * ViewKick * 0.06f, r * 1.02f, r * 0.82f, 10, sk.Glow);
        }
    }
}
