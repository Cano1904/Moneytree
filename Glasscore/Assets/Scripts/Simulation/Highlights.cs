using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    public enum HighlightType : byte
    {
        MultiKnockout,   // 2+ knockouts by one player within 4 s
        ShatterCascade,  // 5+ tiles shattered by one player within 1 s
        GlassHouse,      // knocked yourself out by breaking your own floor
        LongShot,        // projectile knockout from ≥ 40 m
        AirStrike,       // knockout with a shot fired while airborne
    }

    public struct Highlight
    {
        public byte Slot;
        public int Tick;
        public HighlightType Type;
        public int Count;
        public int Score;

        public string Label
        {
            get
            {
                switch (Type)
                {
                    case HighlightType.MultiKnockout: return Count >= 3 ? $"{Count}x MULTI-SHATTER" : "DOUBLE KNOCKOUT";
                    case HighlightType.ShatterCascade: return $"FRACTURE CASCADE x{Count}";
                    case HighlightType.GlassHouse: return "WER IM GLASHAUS SITZT…";
                    case HighlightType.LongShot: return "LONG SHOT";
                    case HighlightType.AirStrike: return "AIR STRIKE";
                    default: return Type.ToString();
                }
            }
        }
    }

    /// <summary>Server-side detector for clip-worthy moments. Scores decide the post-game reel order.</summary>
    public sealed class HighlightTracker
    {
        public const float MultiKnockoutWindow = 4f;
        public const float CascadeWindow = 1f;
        public const int CascadeMinTiles = 5;
        public const float GlassHouseWindow = 3f;
        public const float LongShotDistance = 40f;

        private readonly int _tickRate;
        private readonly List<Highlight> _all = new List<Highlight>();
        private readonly Dictionary<int, List<int>> _koTicks = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> _shatterTicks = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, int> _openMulti = new Dictionary<int, int>();   // slot → index in _all
        private readonly Dictionary<int, int> _openCascade = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _ownFloorBreakTick = new Dictionary<int, int>();

        public HighlightTracker(int tickRate) { _tickRate = tickRate; }

        public IReadOnlyList<Highlight> All => _all;

        public void OnKnockout(int attacker, int victim, int tick, KnockoutCause cause, float shotDistance, bool shotFiredAirborne)
        {
            if (attacker < 0 || attacker == victim)
            {
                if (cause == KnockoutCause.Fall && _ownFloorBreakTick.TryGetValue(victim, out int broke) &&
                    tick - broke <= GlassHouseWindow * _tickRate)
                {
                    Add(victim, tick, HighlightType.GlassHouse, 1, 50);
                }
                return;
            }

            var list = Window(_koTicks, attacker, tick, MultiKnockoutWindow);
            list.Add(tick);
            if (list.Count >= 2) Upsert(_openMulti, attacker, tick, HighlightType.MultiKnockout, list.Count, 40 * (list.Count - 1));
            else _openMulti.Remove(attacker);

            if (cause == KnockoutCause.Damage && shotDistance >= LongShotDistance) Add(attacker, tick, HighlightType.LongShot, 1, 30);
            if (cause == KnockoutCause.Damage && shotFiredAirborne) Add(attacker, tick, HighlightType.AirStrike, 1, 35);
        }

        public void OnTileShattered(int instigator, int tick)
        {
            if (instigator < 0) return;
            var list = Window(_shatterTicks, instigator, tick, CascadeWindow);
            list.Add(tick);
            if (list.Count >= CascadeMinTiles) Upsert(_openCascade, instigator, tick, HighlightType.ShatterCascade, list.Count, 10 * list.Count);
            else _openCascade.Remove(instigator);
        }

        /// <summary>The player broke the tile they were standing on (compression, vault or own shot).</summary>
        public void OnOwnFloorBroken(int slot, int tick) => _ownFloorBreakTick[slot] = tick;

        public List<Highlight> Top(int count)
        {
            var sorted = new List<Highlight>(_all);
            sorted.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Tick.CompareTo(b.Tick));
            if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
            return sorted;
        }

        private List<int> Window(Dictionary<int, List<int>> map, int slot, int tick, float seconds)
        {
            if (!map.TryGetValue(slot, out var list))
            {
                list = new List<int>();
                map[slot] = list;
            }
            int window = (int)(seconds * _tickRate);
            list.RemoveAll(t => tick - t > window);
            return list;
        }

        private void Upsert(Dictionary<int, int> open, int slot, int tick, HighlightType type, int count, int score)
        {
            if (open.TryGetValue(slot, out int idx))
            {
                var h = _all[idx];
                h.Tick = tick;
                h.Count = count;
                h.Score = score;
                _all[idx] = h;
            }
            else
            {
                open[slot] = _all.Count;
                Add(slot, tick, type, count, score);
            }
        }

        private void Add(int slot, int tick, HighlightType type, int count, int score)
        {
            _all.Add(new Highlight { Slot = (byte)slot, Tick = tick, Type = type, Count = count, Score = score });
        }
    }
}
