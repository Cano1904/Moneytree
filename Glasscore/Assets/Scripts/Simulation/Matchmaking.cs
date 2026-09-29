using System;
using System.Collections.Generic;

namespace Glasscore.Simulation
{
    /// <summary>
    /// Skill-based matchmaking search policy. Sessions advertise an MMR bucket (200 wide) as a session
    /// property; a searching client widens its acceptable window over time and probes buckets closest
    /// to its own rating first.
    /// </summary>
    public static class MatchmakingPolicy
    {
        public const int BucketSize = 200;
        public const int InitialWindow = 100;
        public const int WindowStep = 50;
        public const float WindowStepSeconds = 5f;
        public const int MaxWindow = 600;
        public const float OpenSearchAfterSeconds = 60f;
        public const float CreateSessionAfterSeconds = 15f;

        public static int WindowAt(float secondsSearching)
        {
            if (secondsSearching >= OpenSearchAfterSeconds) return int.MaxValue;
            int steps = (int)Math.Floor(secondsSearching / WindowStepSeconds);
            return Math.Min(InitialWindow + steps * WindowStep, MaxWindow);
        }

        public static int BucketOf(int mmr) => Math.Max(0, mmr) / BucketSize;

        /// <summary>Buckets overlapping [mmr - window, mmr + window], nearest first.</summary>
        public static List<int> CandidateBuckets(int mmr, int window, int maxBucket = 20)
        {
            int lo = window == int.MaxValue ? 0 : BucketOf(mmr - window);
            int hi = window == int.MaxValue ? maxBucket : Math.Min(maxBucket, BucketOf(mmr + window));
            int own = BucketOf(mmr);
            var list = new List<int>();
            for (int b = lo; b <= hi; b++) list.Add(b);
            list.Sort((a, b) =>
            {
                int da = Math.Abs(a - own), db = Math.Abs(b - own);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            return list;
        }
    }

    public struct RatingEntry
    {
        public int PlayerId;
        public int Rating;
        public int GamesPlayed;
        /// <summary>1 = winner. Equal placements are ties.</summary>
        public int Placement;
        public bool LeftEarly;
    }

    /// <summary>
    /// Multiplayer Elo: every pair of players is scored as a 1v1 result, K is split across the N-1
    /// opponents. Leavers are ranked last and, in competitive queues, pay an extra penalty.
    /// </summary>
    public static class RatingSystem
    {
        public const int StartingRating = 1000;
        public const int ProvisionalGames = 20;
        public const float ProvisionalK = 48f;
        public const float EstablishedK = 24f;
        public const int LeavePenalty = 15;

        public static Dictionary<int, int> ComputeDeltas(IReadOnlyList<RatingEntry> entries, bool competitive)
        {
            var result = new Dictionary<int, int>();
            int n = entries.Count;
            if (n < 2)
            {
                foreach (var e in entries) result[e.PlayerId] = 0;
                return result;
            }

            int worst = 0;
            foreach (var e in entries) worst = Math.Max(worst, e.Placement);

            for (int i = 0; i < n; i++)
            {
                var a = entries[i];
                int placeA = a.LeftEarly ? worst + 1 : a.Placement;
                float k = (a.GamesPlayed < ProvisionalGames ? ProvisionalK : EstablishedK) / (n - 1);
                double delta = 0;

                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    var b = entries[j];
                    int placeB = b.LeftEarly ? worst + 1 : b.Placement;
                    double expected = 1.0 / (1.0 + Math.Pow(10.0, (b.Rating - a.Rating) / 400.0));
                    double actual = placeA < placeB ? 1.0 : (placeA == placeB ? 0.5 : 0.0);
                    delta += k * (actual - expected);
                }

                int rounded = (int)Math.Round(delta, MidpointRounding.AwayFromZero);
                if (a.LeftEarly && competitive) rounded -= LeavePenalty;
                result[a.PlayerId] = rounded;
            }

            return result;
        }

        /// <summary>Converts final scores into placements (1 = best, ties share a placement).</summary>
        public static int[] PlacementsFromScores(IReadOnlyList<int> scores)
        {
            var placements = new int[scores.Count];
            for (int i = 0; i < scores.Count; i++)
            {
                int better = 0;
                for (int j = 0; j < scores.Count; j++) if (scores[j] > scores[i]) better++;
                placements[i] = better + 1;
            }
            return placements;
        }
    }
}
