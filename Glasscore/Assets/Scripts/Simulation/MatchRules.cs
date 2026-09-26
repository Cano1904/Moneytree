using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    public enum MatchPhase : byte
    {
        Loading,        // arena scene loading on all peers, tile grid being spawned
        Countdown,      // 3-2-1-SHATTER, movement locked, look allowed
        Live,           // 00:00 – 06:00 open combat
        Cascade,        // 06:00 – 08:00 edge erosion ("Dynamic Fracture Cascade")
        Overtime,       // golden knockout, max 60 s
        MatchEnd,       // winner reveal, inputs locked
        Highlights,     // highlight reel + clip export window
        Results,        // scoreboard, rating deltas
        ReturnToLobby,  // server loads the lobby scene
    }

    public enum MatchEventType : byte
    {
        PhaseChanged,
        PressureWave,
        CascadeWarning,
        ErosionWave,
    }

    public enum MatchEndReason : byte
    {
        None,
        ScoreLimit,
        TimeLimit,
        OvertimeDecided,
        OvertimeExpired, // draw
        Forfeit,
        Abandoned,
    }

    public struct MatchEvent
    {
        public MatchEventType Type;
        public MatchPhase Phase;
        public int WaveIndex;
        public int Tick;
    }

    public struct MatchInputs
    {
        public int ConnectedPlayers;
        public bool AllPlayersLoaded;
        public int LeaderScore;
        public bool LeaderTied;
        public int MaxScore;
    }

    public static class MatchTimings
    {
        public const int TickRate = 60;
        public const float LoadingTimeout = 20f;
        public const float Countdown = 5f;
        public const float Live = 360f;
        public const float PressureWaveAt = 180f;
        public const float CascadeWarningAt = 330f;
        public const float Cascade = 120f;
        public const float ErosionInterval = 10f;
        public const int ErosionWaveCount = 12;           // Cascade / ErosionInterval
        public const float ErosionDrainPerSecond = 0.25f; // fraction of max integrity → collapse in 4 s
        public const float ErosionSafeRadius = 3.2f;      // m, the final ring that never erodes
        public const float PressureWaveDamage = 10f;      // to every standard tile
        public const float Overtime = 60f;
        public const float MatchEnd = 4f;
        public const float Highlights = 10f;
        public const float Results = 15f;
        public const float Respawn = 3f;
        public const float SpawnProtection = 2f;
        public const float AttributionWindow = 6f;
        public const int MinPlayers = 2;
        public const int DefaultMaxScore = 10;
        public const int MinMaxScore = 5;
        public const int MaxMaxScore = 25;

        public static int ToTicks(float seconds, int tickRate = TickRate) => (int)Math.Round(seconds * tickRate);
    }

    /// <summary>
    /// Pure, server-side match state machine. MatchDirector (Unity) calls <see cref="Advance"/> once per
    /// forward tick on the state authority and mirrors <see cref="Phase"/>/<see cref="PhaseStartTick"/>
    /// into [Networked] properties for clients.
    /// </summary>
    public sealed class MatchClock
    {
        public readonly int TickRate;
        public MatchPhase Phase { get; private set; }
        public int PhaseStartTick { get; private set; }
        public MatchEndReason EndReason { get; private set; }
        public int ErosionWavesFired { get; private set; }

        private bool _pressureWaveFired;
        private bool _cascadeWarningFired;

        public MatchClock(int startTick, int tickRate = MatchTimings.TickRate)
        {
            TickRate = tickRate;
            Phase = MatchPhase.Loading;
            PhaseStartTick = startTick;
        }

        public float Elapsed(int tick) => (tick - PhaseStartTick) / (float)TickRate;

        public static float PhaseDuration(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Loading: return MatchTimings.LoadingTimeout;
                case MatchPhase.Countdown: return MatchTimings.Countdown;
                case MatchPhase.Live: return MatchTimings.Live;
                case MatchPhase.Cascade: return MatchTimings.Cascade;
                case MatchPhase.Overtime: return MatchTimings.Overtime;
                case MatchPhase.MatchEnd: return MatchTimings.MatchEnd;
                case MatchPhase.Highlights: return MatchTimings.Highlights;
                case MatchPhase.Results: return MatchTimings.Results;
                default: return 0f;
            }
        }

        public static bool IsCombatPhase(MatchPhase phase) =>
            phase == MatchPhase.Live || phase == MatchPhase.Cascade || phase == MatchPhase.Overtime;

        public void Advance(int tick, MatchInputs inputs, List<MatchEvent> events)
        {
            float t = Elapsed(tick);
            switch (Phase)
            {
                case MatchPhase.Loading:
                    if (inputs.ConnectedPlayers == 0) End(tick, MatchEndReason.Abandoned, events, MatchPhase.ReturnToLobby);
                    else if (inputs.AllPlayersLoaded || t >= MatchTimings.LoadingTimeout)
                    {
                        if (inputs.ConnectedPlayers < MatchTimings.MinPlayers) End(tick, MatchEndReason.Abandoned, events, MatchPhase.ReturnToLobby);
                        else Enter(MatchPhase.Countdown, tick, events);
                    }
                    break;

                case MatchPhase.Countdown:
                    if (inputs.ConnectedPlayers < MatchTimings.MinPlayers) End(tick, MatchEndReason.Abandoned, events, MatchPhase.ReturnToLobby);
                    else if (t >= MatchTimings.Countdown) Enter(MatchPhase.Live, tick, events);
                    break;

                case MatchPhase.Live:
                    if (CheckCommonEnd(tick, inputs, events)) break;
                    if (!_pressureWaveFired && t >= MatchTimings.PressureWaveAt)
                    {
                        _pressureWaveFired = true;
                        events.Add(new MatchEvent { Type = MatchEventType.PressureWave, Phase = Phase, Tick = tick });
                    }
                    if (!_cascadeWarningFired && t >= MatchTimings.CascadeWarningAt)
                    {
                        _cascadeWarningFired = true;
                        events.Add(new MatchEvent { Type = MatchEventType.CascadeWarning, Phase = Phase, Tick = tick });
                    }
                    if (t >= MatchTimings.Live) Enter(MatchPhase.Cascade, tick, events);
                    break;

                case MatchPhase.Cascade:
                    if (CheckCommonEnd(tick, inputs, events)) break;
                    int due = Math.Min(MatchTimings.ErosionWaveCount, (int)Math.Floor(t / MatchTimings.ErosionInterval) + 1);
                    while (ErosionWavesFired < due)
                    {
                        events.Add(new MatchEvent { Type = MatchEventType.ErosionWave, Phase = Phase, WaveIndex = ErosionWavesFired, Tick = tick });
                        ErosionWavesFired++;
                    }
                    if (t >= MatchTimings.Cascade)
                    {
                        if (inputs.LeaderTied) Enter(MatchPhase.Overtime, tick, events);
                        else End(tick, MatchEndReason.TimeLimit, events);
                    }
                    break;

                case MatchPhase.Overtime:
                    if (inputs.ConnectedPlayers < MatchTimings.MinPlayers) End(tick, MatchEndReason.Forfeit, events);
                    else if (!inputs.LeaderTied) End(tick, MatchEndReason.OvertimeDecided, events);
                    else if (t >= MatchTimings.Overtime) End(tick, MatchEndReason.OvertimeExpired, events);
                    break;

                case MatchPhase.MatchEnd:
                    if (t >= MatchTimings.MatchEnd) Enter(MatchPhase.Highlights, tick, events);
                    break;

                case MatchPhase.Highlights:
                    if (t >= MatchTimings.Highlights) Enter(MatchPhase.Results, tick, events);
                    break;

                case MatchPhase.Results:
                    if (t >= MatchTimings.Results) Enter(MatchPhase.ReturnToLobby, tick, events);
                    break;

                case MatchPhase.ReturnToLobby:
                    break;
            }
        }

        private bool CheckCommonEnd(int tick, MatchInputs inputs, List<MatchEvent> events)
        {
            if (inputs.ConnectedPlayers < MatchTimings.MinPlayers)
            {
                End(tick, MatchEndReason.Forfeit, events);
                return true;
            }
            if (inputs.LeaderScore >= inputs.MaxScore && !inputs.LeaderTied)
            {
                End(tick, MatchEndReason.ScoreLimit, events);
                return true;
            }
            return false;
        }

        private void End(int tick, MatchEndReason reason, List<MatchEvent> events, MatchPhase next = MatchPhase.MatchEnd)
        {
            EndReason = reason;
            Enter(next, tick, events);
        }

        private void Enter(MatchPhase phase, int tick, List<MatchEvent> events)
        {
            Phase = phase;
            PhaseStartTick = tick;
            events.Add(new MatchEvent { Type = MatchEventType.PhaseChanged, Phase = phase, Tick = tick });
        }
    }

    /// <summary>Outer-rings-first collapse schedule for the Cascade phase.</summary>
    public static class ErosionPlanner
    {
        public static float MaxRadius(TileLayout layout)
        {
            float max = 0f;
            for (int i = 0; i < layout.Count; i++) max = Math.Max(max, layout.RadialDistance(i));
            return max;
        }

        public static float WaveRadius(float maxRadius, int waveIndex, int waveCount = MatchTimings.ErosionWaveCount)
        {
            float t = (waveIndex + 1) / (float)waveCount;
            return GcMath.Lerp(maxRadius, MatchTimings.ErosionSafeRadius, GcMath.Clamp01(t));
        }

        /// <summary>Marks and returns every intact, not-yet-destabilized tile outside this wave's radius.</summary>
        public static int CollectWave(TileLayout layout, ITileStore store, bool[] destabilized, float maxRadius, int waveIndex, List<int> output)
        {
            output.Clear();
            float radius = WaveRadius(maxRadius, waveIndex);
            for (int i = 0; i < layout.Count; i++)
            {
                if (destabilized[i] || store.GetIntegrity(i) <= 0f) continue;
                if (layout.RadialDistance(i) > radius)
                {
                    destabilized[i] = true;
                    output.Add(i);
                }
            }
            return output.Count;
        }

        public static float DrainPerTick(float maxIntegrity, float dt) => maxIntegrity * MatchTimings.ErosionDrainPerSecond * dt;
    }

    /// <summary>Last-hit credit: a knockout counts for whoever damaged the victim within the window.</summary>
    public sealed class KillAttribution
    {
        public const int NoAttacker = -1;
        public const int KnockoutPoints = 1;
        public const int SelfKnockoutPenalty = 1;

        private readonly int _windowTicks;
        private readonly Dictionary<int, (int attacker, int tick)> _lastHit = new Dictionary<int, (int, int)>();

        public KillAttribution(int windowTicks) { _windowTicks = windowTicks; }

        public void RecordHit(int victim, int attacker, int tick)
        {
            if (attacker == victim || attacker < 0) return;
            _lastHit[victim] = (attacker, tick);
        }

        public int ResolveKnockout(int victim, int tick)
        {
            if (!_lastHit.TryGetValue(victim, out var hit)) return NoAttacker;
            _lastHit.Remove(victim);
            return tick - hit.tick <= _windowTicks ? hit.attacker : NoAttacker;
        }

        public void Forget(int player) => _lastHit.Remove(player);

        /// <summary>Self-knockouts ("wer im Glashaus sitzt…") cost a point, never going below zero.</summary>
        public static int ApplySelfKnockout(int score) => Math.Max(0, score - SelfKnockoutPenalty);
    }

    public static class SpawnSelector
    {
        /// <summary>
        /// Picks the intact spawn pad farthest from the nearest enemy. Falls back to the healthiest
        /// intact tile if every pad is gone. Returns -1 only if the whole arena has collapsed.
        /// </summary>
        public static int Select(TileLayout layout, ITileStore store, IReadOnlyList<Vec3> enemyPositions, ulong tiebreakSeed)
        {
            int best = -1;
            float bestScore = float.MinValue;
            ulong bestTie = 0;

            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    float integrity = store.GetIntegrity(i);
                    if (integrity <= 0f) continue;
                    if (pass == 0 && !layout.IsSpawnPad[i]) continue;

                    float nearest = 1000f;
                    for (int e = 0; e < enemyPositions.Count; e++)
                        nearest = Math.Min(nearest, (enemyPositions[e] - layout.Centers[i]).Length);

                    float health = integrity / layout.MaxIntegrity(i);
                    float score = pass == 0 ? nearest : health * 100f + nearest;
                    ulong tie = DeterministicHash.Mix(tiebreakSeed, (ulong)i);

                    if (score > bestScore + 1e-4f || (Math.Abs(score - bestScore) <= 1e-4f && tie > bestTie))
                    {
                        best = i;
                        bestScore = score;
                        bestTie = tie;
                    }
                }
            }
            return best;
        }
    }
}
