using Glasscore.Net;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>In-match HUD, the multiplayer Pause menu, and the post-game highlights / results screen.</summary>
    public sealed class MatchScreens
    {
        private static readonly string[] PauseItems = { "RESUME", "OPTIONS", "LEAVE MATCH" };
        private int _pauseFocus;
        private bool _confirmLeave;

        public void Draw(GameApp app)
        {
            GameClient c = app.Client;
            MatchPresenter p = app.Presenter;
            if (c == null || p == null) return;

            MatchPhase phase = c.Phase;
            bool postGame = phase == MatchPhase.MatchEnd || phase == MatchPhase.Highlights || phase == MatchPhase.Results || phase == MatchPhase.ReturnToLobby;

            if (!app.Paused && !app.SettingsOpen) DrawHud(app, c, p);
            if (postGame && phase != MatchPhase.MatchEnd) DrawResults(app, c, p);
            if (app.Paused && !app.SettingsOpen) DrawPause(app, c);
        }

        // ───────────────────────────── HUD ─────────────────────────────

        private static void DrawHud(GameApp app, GameClient c, MatchPresenter p)
        {
            float W = Ui.Width, H = Ui.Height;
            PlayerState me = c.LocalPlayer;
            MatchPhase phase = c.Phase;

            // Name tags (world → screen).
            Camera cam = p.Camera;
            for (int i = 0; i < GameWorld.MaxPlayers; i++)
            {
                if (i == c.LocalSlot) continue;
                PlayerState o = c.RenderPlayers[i];
                if (!o.Active || !o.Alive) continue;
                Vector3 world = o.Position.ToUnity() + Vector3.up * 2.15f;
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;
                float gx = sp.x / Ui.Scale, gy = (Screen.height - sp.y) / Ui.Scale;
                string name = p.NameOf(i) + (app.Voice.IsTalking((byte)i) ? "  ◉" : string.Empty);
                Color col = Cosmetics.SlotColor(i);
                Ui.Label(new Rect(gx - 150, gy - 34, 300, 30), name, 20, col, TextAnchor.MiddleCenter);
                Ui.Bar(new Rect(gx - 40, gy - 4, 80, 5), o.Health / PlayerRules.MaxHealth, col);
            }

            // Timer + phase (top centre).
            float remaining = c.PhaseTimeRemaining;
            string phaseName = PhaseLabel(phase);
            Ui.Label(new Rect(W / 2 - 200, 18, 400, 60), FormatTime(remaining), 46, phase == MatchPhase.Cascade || phase == MatchPhase.Overtime ? Ui.Red : Color.white, TextAnchor.MiddleCenter, glow: true);
            Ui.Label(new Rect(W / 2 - 300, 72, 600, 30), phaseName, 20, Ui.Cyan, TextAnchor.MiddleCenter);

            if (phase == MatchPhase.Countdown)
            {
                int n = Mathf.CeilToInt(remaining);
                string t = n > 3 ? "GET READY" : (n > 0 ? n.ToString() : "SHATTER!");
                Ui.Label(new Rect(W / 2 - 400, H / 2 - 160, 800, 160), t, n > 3 ? 64 : 140, Ui.Cyan, TextAnchor.MiddleCenter, glow: true);
                CountdownBeeps(n);
            }
            else if (phase == MatchPhase.Loading)
            {
                Ui.Label(new Rect(W / 2 - 500, H / 2 - 40, 1000, 80), "Waiting for all players to load the arena…", 32, Color.white, TextAnchor.MiddleCenter);
            }

            // Banner.
            if (!string.IsNullOrEmpty(p.Banner) && Time.time < p.BannerUntil)
                Ui.Label(new Rect(W / 2 - 700, 150, 1400, 70), p.Banner, 42, p.BannerColor, TextAnchor.MiddleCenter, glow: true);

            // Scoreboard (top right).
            DrawScoreboard(c, p, new Rect(W - 380, 20, 360, 40));

            // Kill feed (top left).
            for (int i = 0; i < p.Feed.Count; i++)
            {
                var f = p.Feed[i];
                float age = Time.time - f.Time;
                if (age > 6f) continue;
                var col = f.Color;
                col.a = Mathf.Clamp01(6f - age);
                Ui.Label(new Rect(24, 24 + i * 32, 800, 30), f.Text, 20, col);
            }

            if (me.Alive)
            {
                // Crosshair + hit marker.
                Vector2 c0 = new Vector2(W / 2, H / 2);
                Color ch = new Color(0f, 1f, 1f, 0.9f);
                Ui.Rect(new Rect(c0.x - 1.5f, c0.y - 12, 3, 8), ch);
                Ui.Rect(new Rect(c0.x - 1.5f, c0.y + 4, 3, 8), ch);
                Ui.Rect(new Rect(c0.x - 12, c0.y - 1.5f, 8, 3), ch);
                Ui.Rect(new Rect(c0.x + 4, c0.y - 1.5f, 8, 3), ch);
                if (Time.time < p.HitMarkerUntil)
                {
                    Ui.Label(new Rect(c0.x - 30, c0.y - 30, 60, 60), "✕", 44, Color.white, TextAnchor.MiddleCenter);
                }

                // Health (bottom left).
                Ui.Label(new Rect(40, H - 130, 300, 40), "INTEGRITY", 18, new Color(1, 1, 1, 0.6f));
                Ui.Bar(new Rect(40, H - 92, 360, 22), me.Health / PlayerRules.MaxHealth, me.Health > 35 ? Ui.Cyan : Ui.Red);
                Ui.Label(new Rect(410, H - 100, 120, 40), Mathf.CeilToInt(me.Health).ToString(), 30, Color.white);
                if (me.SpawnProtection > 0f) Ui.Label(new Rect(40, H - 170, 500, 34), "SPAWN PROTECTION", 20, Ui.Green);

                // Tile under your feet: the glass house you are standing in.
                if (me.Grounded && me.GroundTile >= 0 && c.Tiles != null)
                {
                    GlassType gt = c.Match.Layout.Types[me.GroundTile];
                    float integ = c.Tiles.GetIntegrity(me.GroundTile);
                    float max = GlassCatalog.Get(gt).MaxIntegrity;
                    bool weak = GlassCatalog.IsWeakened(gt, integ);
                    Ui.Label(new Rect(40, H - 60, 520, 30), $"FLOOR: {GlassName(gt)}  {Mathf.CeilToInt(integ)}/{max:0}" + (weak ? "  — WEAKENED!" : string.Empty), 18, weak ? Ui.Red : Mats.GlassEdge(gt));
                    Ui.Bar(new Rect(40, H - 30, 360, 8), integ / max, weak ? Ui.Red : Mats.GlassEdge(gt));
                }

                // Weapon + cooldowns (bottom right).
                WeaponSpec w = WeaponCatalog.Get(me.Weapon);
                Ui.Label(new Rect(W - 640, H - 140, 600, 40), w.DisplayName, 26, Color.white, TextAnchor.MiddleRight);
                Ui.Label(new Rect(W - 640, H - 104, 600, 30), $"recoil Δv {w.RecoilDeltaV:0.0} m/s  ·  {(w.Heavy ? "HEAVY" : "light")}", 18, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);
                Ui.Bar(new Rect(W - 400, H - 66, 360, 10), 1f - me.FireCooldown / Mathf.Max(0.01f, w.FireInterval), Ui.Cyan);
                string anchor = me.Anchored ? "ANCHORED" : (me.AnchorCooldown > 0f ? $"ANCHOR {me.AnchorCooldown:0.0}s" : "ANCHOR READY");
                Ui.Label(new Rect(W - 640, H - 50, 600, 30), anchor, 18, me.Anchored ? Ui.Magenta : (me.AnchorCooldown > 0f ? new Color(1, 1, 1, 0.5f) : Ui.Green), TextAnchor.MiddleRight);
                for (int i = 0; i < WeaponCatalog.Count; i++)
                    Ui.Rect(new Rect(W - 400 + i * 92, H - 84, 84, 6), i == me.Weapon ? Ui.Cyan : new Color(1, 1, 1, 0.2f));
            }
            else if (MatchClock.IsCombatPhase(phase) && me.Active)
            {
                float left = Mathf.Max(0f, p.LocalRespawnAt - Time.time);
                string killer = p.LastKillerSlot >= 0 ? $"Shattered by {p.NameOf(p.LastKillerSlot)}" : "You broke your own glass house";
                Ui.Label(new Rect(W / 2 - 500, H / 2 - 90, 1000, 70), killer, 36, Ui.Red, TextAnchor.MiddleCenter, glow: true);
                Ui.Label(new Rect(W / 2 - 500, H / 2 - 20, 1000, 50), left > 0f ? $"Respawning in {left:0.0}" : "Waiting for a safe spawn pad…", 26, Color.white, TextAnchor.MiddleCenter);
            }

            // Damage flash.
            if (Time.time < p.DamageFlashUntil)
                Ui.Rect(new Rect(0, 0, W, H), new Color(1f, 0.1f, 0.15f, 0.18f * (p.DamageFlashUntil - Time.time) / 0.25f));

            // Voice + ping (bottom centre).
            string mic = !app.Settings.VoiceEnabled ? "VOICE OFF" : app.Voice.Muted ? "MIC MUTED" : app.Voice.Transmitting ? "● TALKING" : (app.Settings.PushToTalk ? "HOLD V TO TALK" : "OPEN MIC");
            Ui.Label(new Rect(W / 2 - 300, H - 44, 600, 30), $"{mic}   ·   {c.PingMs} ms", 18, app.Voice.Transmitting ? Ui.Green : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        }

        private static int _lastBeep = -1;

        private static void CountdownBeeps(int n)
        {
            if (Event.current.type != EventType.Repaint || n == _lastBeep) return;
            _lastBeep = n;
            if (n >= 1 && n <= 3) AudioService.Instance?.Play(AudioService.Beep);
        }

        private static void DrawScoreboard(GameClient c, MatchPresenter p, Rect first)
        {
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < GameWorld.MaxPlayers; i++) if (c.RenderPlayers[i].Active) order.Add(i);
            order.Sort((a, b) => c.Scores[b].CompareTo(c.Scores[a]));
            int target = c.Match != null ? c.Match.MaxScore : MatchTimings.DefaultMaxScore;
            if (c.Match != null && c.Match.Teams)
            {
                int t0 = 0, t1 = 0;
                foreach (int i in order) { if (c.RenderPlayers[i].Team == 0) t0 += c.Scores[i]; else t1 += c.Scores[i]; }
                Ui.Label(new Rect(first.x, first.y, first.width, 36), $"CYAN {t0}  ·  MAGENTA {t1}   (to {target})", 22, Color.white, TextAnchor.MiddleRight);
                first.y += 40;
            }
            else Ui.Label(new Rect(first.x, first.y, first.width, 30), $"FIRST TO {target}", 18, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                var r = new Rect(first.x, first.y + 30 + k * 34, first.width, 32);
                Color col = Cosmetics.SlotColor(i);
                if (i == c.LocalSlot) Ui.Rect(r, new Color(col.r, col.g, col.b, 0.15f));
                Ui.Label(new Rect(r.x + 10, r.y, r.width - 70, r.height), p.NameOf(i), 20, col);
                Ui.Label(new Rect(r.xMax - 60, r.y, 50, r.height), c.Scores[i].ToString(), 22, Color.white, TextAnchor.MiddleRight);
            }
        }

        // ───────────────────────────── pause ─────────────────────────────

        private void DrawPause(GameApp app, GameClient c)
        {
            float W = Ui.Width, H = Ui.Height;
            // Live match continues underneath (timeScale 1); the camera applies the Gaussian blur.
            Color pulse = new Color(1f, 1f, 1f, Ui.Pulse(3f, 0.45f));
            Ui.Label(new Rect(W / 2 - 500, 200, 1000, 80), "MATCH IN PROGRESS", 56, pulse, TextAnchor.MiddleCenter, glow: true);
            Ui.Label(new Rect(W / 2 - 500, 280, 1000, 40), $"Latency {c.PingMs} ms  ·  {PhaseLabel(c.Phase)}  ·  {FormatTime(c.PhaseTimeRemaining)}", 24, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);

            if (_confirmLeave)
            {
                bool competitive = c.Lobby.Settings.Competitive;
                var box = new Rect(W / 2 - 420, 400, 840, 260);
                Ui.PanelBox(box, "LEAVE MATCH?");
                Ui.Label(new Rect(box.x + 30, box.y + 70, box.width - 60, 60),
                    competitive ? "This is a competitive match: leaving counts as a loss and costs extra rating." : "You will return to the main menu.", 22, competitive ? Ui.Red : Color.white);
                if (Ui.NeonButton("pause.leave.yes", new Rect(box.x + 30, box.yMax - 90, 360, 64), "LEAVE", 28, false, true, Ui.Red))
                {
                    _confirmLeave = false;
                    app.LeaveToMenu();
                }
                if (Ui.NeonButton("pause.leave.no", new Rect(box.xMax - 390, box.yMax - 90, 360, 64), "STAY", 28) || app.Input.MenuBack)
                    _confirmLeave = false;
                return;
            }

            _pauseFocus = Ui.NavigateFocus(_pauseFocus, PauseItems.Length, app.Input);
            bool confirm = app.Input.MenuConfirm;
            for (int i = 0; i < PauseItems.Length; i++)
            {
                var r = new Rect(W / 2 - 220, 400 + i * 90, 440, 70);
                bool clicked = Ui.NeonButton("pause." + i, r, PauseItems[i], 30, app.Input.UsingGamepad && _pauseFocus == i, true, i == 2 ? Ui.Red : (Color?)null);
                if (clicked || (confirm && _pauseFocus == i))
                {
                    if (confirm) Ui.Click();
                    switch (i)
                    {
                        case 0: app.SetPaused(false); break;
                        case 1: app.OpenSettings(); break;
                        case 2: _confirmLeave = true; break;
                    }
                }
            }
        }

        // ───────────────────────────── post game ─────────────────────────────

        private static void DrawResults(GameApp app, GameClient c, MatchPresenter p)
        {
            float W = Ui.Width;
            var panel = new Rect(W / 2 - 820, 110, 1640, 860);
            Ui.PanelBox(panel);
            string title = c.EndReason == MatchEndReason.OvertimeExpired ? "DRAW" : "MATCH RESULTS";
            Ui.Label(new Rect(panel.x + 30, panel.y + 14, 800, 70), title, 50, Ui.Cyan, TextAnchor.MiddleLeft, glow: true);
            Ui.Label(new Rect(panel.xMax - 630, panel.y + 24, 600, 50), $"Back to lobby in {Mathf.CeilToInt(c.Phase == MatchPhase.Highlights ? c.PhaseTimeRemaining + MatchTimings.Results : c.PhaseTimeRemaining)}s", 22, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);

            // Scoreboard with rating changes.
            float x = panel.x + 30, y = panel.y + 110;
            string[] headers = { "#", "PLAYER", "SCORE", "KO", "FALLS", "TILES", "RATING" };
            float[] cols = { 0, 60, 460, 580, 680, 800, 920 };
            for (int h = 0; h < headers.Length; h++) Ui.Label(new Rect(x + cols[h], y, 140, 30), headers[h], 18, Ui.Cyan);
            y += 38;
            if (c.Results.Count == 0)
            {
                Ui.Label(new Rect(x, y, 900, 40), "Tallying results…", 24, new Color(1, 1, 1, 0.7f));
            }
            foreach (ResultRow row in c.Results)
            {
                Color col = Cosmetics.SlotColor(row.Slot);
                if (row.Slot == c.LocalSlot) Ui.Rect(new Rect(x - 10, y, 1080, 40), new Color(col.r, col.g, col.b, 0.14f));
                Ui.Label(new Rect(x + cols[0], y, 60, 40), row.Placement.ToString(), 26, row.Placement == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white);
                Ui.Label(new Rect(x + cols[1], y, 390, 40), row.Name, 24, col);
                Ui.Label(new Rect(x + cols[2], y, 100, 40), row.Score.ToString(), 24, Color.white);
                Ui.Label(new Rect(x + cols[3], y, 100, 40), row.Knockouts.ToString(), 22, Color.white);
                Ui.Label(new Rect(x + cols[4], y, 100, 40), row.Deaths.ToString(), 22, Color.white);
                Ui.Label(new Rect(x + cols[5], y, 100, 40), row.TilesShattered.ToString(), 22, Color.white);
                string delta = (row.RatingDelta >= 0 ? "+" : string.Empty) + row.RatingDelta;
                Ui.Label(new Rect(x + cols[6], y, 180, 40), $"{row.NewRating} ({delta})", 22, row.RatingDelta >= 0 ? Ui.Green : Ui.Red);
                y += 44;
            }

            // Highlights + social clip export.
            var hl = new Rect(panel.x + 1120, panel.y + 110, 490, 700);
            Ui.Label(new Rect(hl.x, hl.y, hl.width, 30), "HIGHLIGHT REEL", 20, Ui.Cyan);
            var markers = app.Clips.Markers;
            if (markers.Count == 0) Ui.Label(new Rect(hl.x, hl.y + 40, hl.width, 60), "No highlights this round.", 20, new Color(1, 1, 1, 0.6f));
            for (int i = 0; i < markers.Count && i < 6; i++)
            {
                var r = new Rect(hl.x, hl.y + 40 + i * 92, hl.width, 84);
                Ui.Rect(r, new Color(0f, 1f, 1f, 0.06f));
                Ui.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, 36), markers[i].Label, 20, Color.white);
                if (Ui.NeonButton("clip." + i, new Rect(r.x + 12, r.y + 42, 220, 36), "EXPORT CLIP", 16))
                {
                    string path = app.Clips.Export(markers[i].Time, markers[i].Label);
                    app.ShowNotice(path != null ? app.Clips.Status : "Clip buffer empty.", 6f);
                }
            }
            if (Ui.NeonButton("clip.last", new Rect(hl.x, hl.yMax - 120, 240, 44), "SAVE LAST 10 s", 16))
            {
                app.Clips.Export(Time.unscaledTimeAsDouble - 2.0, "last_moments");
                app.ShowNotice(app.Clips.Status, 6f);
            }
            if (Ui.NeonButton("clip.folder", new Rect(hl.x + 250, hl.yMax - 120, 240, 44), "OPEN FOLDER", 16)) ClipRecorder.OpenClipFolder();
            if (!string.IsNullOrEmpty(app.Clips.Status)) Ui.Label(new Rect(hl.x, hl.yMax - 66, hl.width, 60), app.Clips.Status, 14, new Color(1, 1, 1, 0.6f));
        }

        // ───────────────────────────── helpers ─────────────────────────────

        private static string PhaseLabel(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Loading: return "LOADING ARENA";
                case MatchPhase.Countdown: return "COUNTDOWN";
                case MatchPhase.Live: return "LIVE";
                case MatchPhase.Cascade: return "FRACTURE CASCADE";
                case MatchPhase.Overtime: return "OVERTIME";
                case MatchPhase.MatchEnd: return "MATCH OVER";
                case MatchPhase.Highlights: return "HIGHLIGHTS";
                case MatchPhase.Results: return "RESULTS";
                default: return "RETURNING TO LOBBY";
            }
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60:0}:{s % 60:00}";
        }

        private static string GlassName(GlassType t)
        {
            switch (t)
            {
                case GlassType.Standard: return "FLOAT GLASS";
                case GlassType.Tempered: return "TEMPERED";
                case GlassType.Reinforced: return "BULLETPROOF";
                default: return "—";
            }
        }
    }
}
