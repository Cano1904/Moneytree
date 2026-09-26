using System;
using System.Collections.Generic;

namespace ShortLegs.Core
{
    /// <summary>A statement after the server has judged it.</summary>
    public sealed class StatementRecord
    {
        public int Index;
        public Statement Statement;
        /// <summary>Weighted sum of discovered evidence this statement contradicts.</summary>
        public float DeceptionIndex;
        /// <summary>Contradicted by evidence nobody has found yet — the lie is waiting to catch up.</summary>
        public bool IsLatent;
        /// <summary>Punished: a Shrink Event has fired for this statement.</summary>
        public bool IsExposedLie;
        public string ExposedByClueId;
        /// <summary>Exposed only because of a planted clue: the speaker was framed.</summary>
        public bool WasFramed;
    }

    /// <summary>Emitted once per exposed statement. The server turns this into a Shrink Event.</summary>
    public readonly struct LieDetection
    {
        public readonly StatementRecord Record;
        public readonly string ClueId;
        public int SpeakerId => Record.Statement.SpeakerId;

        public LieDetection(StatementRecord record, string clueId)
        {
            Record = record;
            ClueId = clueId;
        }
    }

    /// <summary>
    /// Server-side lie detector (GDD §1.3, §5.1). Compares statements against the evidence found so far.
    /// Lies against undiscovered evidence stay latent and are exposed the moment that evidence is found —
    /// "Lügen haben kurze Beine".
    /// </summary>
    public sealed class DeceptionEngine
    {
        public const float DefaultLieThreshold = 1f;

        private readonly Dictionary<string, Clue> _clues = new Dictionary<string, Clue>();
        private readonly HashSet<string> _discovered = new HashSet<string>();
        private readonly List<StatementRecord> _log = new List<StatementRecord>();

        public float LieThreshold { get; }

        /// <summary>
        /// Multiplayer: true — finding a clue immediately exposes every lie it contradicts.
        /// Story Mode: false — the detective must Present the clue against the statement.
        /// </summary>
        public bool AutoResolveOnDiscovery { get; }

        public IReadOnlyList<StatementRecord> Log => _log;

        public DeceptionEngine(IEnumerable<Clue> clues, bool autoResolveOnDiscovery = true,
            float lieThreshold = DefaultLieThreshold)
        {
            AutoResolveOnDiscovery = autoResolveOnDiscovery;
            LieThreshold = lieThreshold;
            if (clues != null)
                foreach (var c in clues) RegisterClue(c);
        }

        public void RegisterClue(Clue clue)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id)) throw new ArgumentException("Clue needs an id");
            if (_clues.ContainsKey(clue.Id)) throw new ArgumentException($"Duplicate clue id '{clue.Id}'");
            _clues.Add(clue.Id, clue);
            // A new hidden clue can turn honest-looking statements latent.
            foreach (var r in _log) if (!r.IsExposedLie) r.IsLatent = HasHiddenContradiction(r.Statement.Claim);
        }

        public bool TryGetClue(string id, out Clue clue) => _clues.TryGetValue(id, out clue);
        public bool IsDiscovered(string clueId) => _discovered.Contains(clueId);

        public int DiscoveredTrueClueCount
        {
            get
            {
                int n = 0;
                foreach (var id in _discovered)
                    if (_clues[id].IsTrueClue && !_clues[id].IsPlanted) n++;
                return n;
            }
        }

        /// <summary>Judge a new statement. Returns the record; if it is an immediate lie, <paramref name="detection"/> is set.</summary>
        public StatementRecord SubmitStatement(Statement statement, out LieDetection? detection)
        {
            var record = new StatementRecord { Index = _log.Count, Statement = statement };
            _log.Add(record);

            string worstClue = Evaluate(record);
            detection = null;
            if (record.DeceptionIndex >= LieThreshold)
                detection = Expose(record, worstClue);

            return record;
        }

        /// <summary>Mark a clue as found. In auto mode, returns every lie it exposes.</summary>
        public IReadOnlyList<LieDetection> DiscoverClue(string clueId)
        {
            var exposed = new List<LieDetection>();
            if (!_clues.ContainsKey(clueId) || !_discovered.Add(clueId)) return exposed;

            foreach (var r in _log)
            {
                if (r.IsExposedLie) continue;
                string worstClue = Evaluate(r);
                if (AutoResolveOnDiscovery && r.DeceptionIndex >= LieThreshold)
                    exposed.Add(Expose(r, worstClue));
            }
            return exposed;
        }

        /// <summary>
        /// "Present Evidence": an investigator binds a discovered clue to a specific statement.
        /// Only that clue's facts count, so a wrong presentation never exposes anything.
        /// </summary>
        public bool PresentEvidence(int statementIndex, string clueId, out LieDetection detection)
        {
            detection = default;
            if (statementIndex < 0 || statementIndex >= _log.Count) return false;
            if (!_discovered.Contains(clueId) || !_clues.TryGetValue(clueId, out var clue)) return false;

            var record = _log[statementIndex];
            if (record.IsExposedLie) return false;

            float index = 0f;
            foreach (var f in clue.Facts)
                if (FactClaim.Contradicts(record.Statement.Claim, f.Fact)) index += f.Weight;

            if (index < LieThreshold) return false;
            record.DeceptionIndex = Math.Max(record.DeceptionIndex, index);
            detection = Expose(record, clueId);
            return true;
        }

        /// <summary>Recomputes DeceptionIndex/IsLatent; returns the heaviest contradicting discovered clue.</summary>
        private string Evaluate(StatementRecord r)
        {
            float index = 0f, worst = 0f;
            string worstClue = null;
            foreach (var id in _discovered)
            {
                float clueWeight = 0f;
                foreach (var f in _clues[id].Facts)
                    if (FactClaim.Contradicts(r.Statement.Claim, f.Fact)) clueWeight += f.Weight;
                index += clueWeight;
                // Prefer a genuine clue as the "exposer" so framing is only reported when it is the sole cause.
                bool better = clueWeight > worst || (clueWeight > 0f && clueWeight == worst && !_clues[id].IsPlanted);
                if (better) { worst = clueWeight; worstClue = id; }
            }
            r.DeceptionIndex = index;
            r.IsLatent = index < LieThreshold && HasHiddenContradiction(r.Statement.Claim);
            return worstClue;
        }

        private bool HasHiddenContradiction(in FactClaim claim)
        {
            foreach (var kv in _clues)
            {
                if (_discovered.Contains(kv.Key)) continue;
                foreach (var f in kv.Value.Facts)
                    if (FactClaim.Contradicts(claim, f.Fact)) return true;
            }
            return false;
        }

        private LieDetection Expose(StatementRecord r, string clueId)
        {
            r.IsExposedLie = true;
            r.IsLatent = false;
            r.ExposedByClueId = clueId;
            r.WasFramed = !GenuineEvidenceSuffices(r);
            return new LieDetection(r, clueId);
        }

        /// <summary>True if genuine (non-planted) discovered evidence alone reaches the threshold.</summary>
        private bool GenuineEvidenceSuffices(StatementRecord r)
        {
            float genuine = 0f;
            foreach (var id in _discovered)
            {
                var c = _clues[id];
                if (c.IsPlanted) continue;
                foreach (var f in c.Facts)
                    if (FactClaim.Contradicts(r.Statement.Claim, f.Fact)) genuine += f.Weight;
            }
            return genuine >= LieThreshold;
        }
    }
}
