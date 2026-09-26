using System;
using System.Collections.Generic;
using System.Text;

namespace Glasscore.Simulation
{
    /// <summary>6-character friend codes. Alphabet drops I, O, 0 and 1 to avoid misreads.</summary>
    public static class LobbyCode
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 32 symbols → 32^6 ≈ 1.07e9 codes

        public static string Generate(ref DeterministicRandom rng)
        {
            var sb = new StringBuilder(Length);
            for (int i = 0; i < Length; i++) sb.Append(Alphabet[rng.Range(0, Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>Upper-cases and strips whitespace/dashes so "ab3-k9z" and "AB3K9Z" join the same lobby.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                if (char.IsWhiteSpace(ch) || ch == '-') continue;
                sb.Append(char.ToUpperInvariant(ch));
            }
            return sb.ToString();
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length) return false;
            foreach (char ch in code) if (Alphabet.IndexOf(ch) < 0) return false;
            return true;
        }
    }

    public static class LobbyRules
    {
        public const int MinPlayerLimit = 2;
        public const int MaxPlayerLimit = 8;
        public const float StartCountdownSeconds = 3f;
        public const float QuickMatchAutoStartSeconds = 20f;
        public const int PlayerNameMaxLength = 16;

        /// <summary>The host can never lower the limit below the number of players already inside.</summary>
        public static int ClampPlayerLimit(int requested, int currentPlayers) =>
            GcMath.Clamp(Math.Max(requested, currentPlayers), MinPlayerLimit, MaxPlayerLimit);

        public static int ClampMaxScore(int requested) =>
            GcMath.Clamp(requested, MatchTimings.MinMaxScore, MatchTimings.MaxMaxScore);

        /// <summary>Every connected client — the host included — must be READY, with at least two players.</summary>
        public static bool CanStart(IReadOnlyList<bool> readyFlags)
        {
            if (readyFlags.Count < MinPlayerLimit) return false;
            for (int i = 0; i < readyFlags.Count; i++) if (!readyFlags[i]) return false;
            return true;
        }

        /// <summary>Host migrates to the longest-connected remaining player (lowest join order).</summary>
        public static int NextHost(IReadOnlyList<(int player, int joinOrder)> remaining)
        {
            int best = -1, bestOrder = int.MaxValue;
            foreach (var (player, order) in remaining)
            {
                if (order < bestOrder) { best = player; bestOrder = order; }
            }
            return best;
        }

        public static string SanitizePlayerName(string name)
        {
            string clean = ChatFilter.StripMarkup(name ?? string.Empty).Trim();
            if (clean.Length > PlayerNameMaxLength) clean = clean.Substring(0, PlayerNameMaxLength);
            return clean.Length == 0 ? "Glazier" : clean;
        }

        public enum PingQuality { Good, Fair, Poor }

        public static PingQuality ClassifyPing(int milliseconds) =>
            milliseconds < 60 ? PingQuality.Good : (milliseconds < 120 ? PingQuality.Fair : PingQuality.Poor);
    }

    /// <summary>Server-side chat sanitation and per-player rate limiting.</summary>
    public sealed class ChatFilter
    {
        public const int MaxLength = 120;
        public const float MinInterval = 0.5f;
        public const int BurstLimit = 5;
        public const float BurstWindow = 5f;

        private readonly Dictionary<int, Queue<double>> _history = new Dictionary<int, Queue<double>>();
        private readonly HashSet<string> _blockedWords;

        public ChatFilter(IEnumerable<string> blockedWords = null)
        {
            _blockedWords = new HashSet<string>(blockedWords ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Returns the cleaned message, or null if it must be dropped (spam / empty).</summary>
        public string Process(int player, string message, double now)
        {
            if (!_history.TryGetValue(player, out var times))
            {
                times = new Queue<double>();
                _history[player] = times;
            }
            while (times.Count > 0 && now - times.Peek() > BurstWindow) times.Dequeue();
            if (times.Count >= BurstLimit) return null;
            if (times.Count > 0 && now - LastOf(times) < MinInterval) return null;

            string clean = Clean(message);
            if (clean.Length == 0) return null;
            times.Enqueue(now);
            return clean;
        }

        public void Forget(int player) => _history.Remove(player);

        public string Clean(string message)
        {
            string s = StripMarkup(message ?? string.Empty);
            var sb = new StringBuilder(s.Length);
            bool lastSpace = false;
            foreach (char ch in s)
            {
                bool space = char.IsWhiteSpace(ch);
                if (!space && char.IsControl(ch)) continue;
                if (space && lastSpace) continue;
                sb.Append(space ? ' ' : ch);
                lastSpace = space;
            }
            s = sb.ToString().Trim();
            if (s.Length > MaxLength) s = s.Substring(0, MaxLength);
            if (_blockedWords.Count == 0) return s;

            string[] words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (_blockedWords.Contains(words[i])) words[i] = new string('*', words[i].Length);
            return string.Join(" ", words);
        }

        /// <summary>Removes &lt;tags&gt; and stray angle brackets so players cannot inject rich-text markup.</summary>
        public static string StripMarkup(string s)
        {
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char ch = s[i];
                if (ch == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    if (close < 0) { i++; continue; }  // stray '<'
                    i = close + 1;                     // drop the whole tag
                    continue;
                }
                if (ch != '>') sb.Append(ch);
                i++;
            }
            return sb.ToString();
        }

        private static double LastOf(Queue<double> q)
        {
            double last = 0;
            foreach (double t in q) last = t;
            return last;
        }
    }
}
