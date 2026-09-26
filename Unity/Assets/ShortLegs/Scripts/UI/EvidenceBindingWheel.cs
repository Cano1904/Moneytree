using System.Collections.Generic;
using ShortLegs.Cases;
using ShortLegs.Core;
using ShortLegs.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortLegs.UI
{
    /// <summary>
    /// The Evidence Binding Wheel (GDD §5.1): three rings — SUBJECT · CLAIM · VALUE — plus an optional
    /// evidence slot. Produces a StatementPacket; the server alone decides whether it is a lie.
    /// </summary>
    public sealed class EvidenceBindingWheel : MonoBehaviour
    {
        [SerializeField] private CaseDefinition caseDefinition;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text subjectLabel, claimLabel, valueLabel, timeLabel, evidenceLabel, previewLabel;
        [SerializeField] private Button subjectPrev, subjectNext, claimPrev, claimNext, valuePrev, valueNext, timePrev, timeNext, evidenceNext;
        [SerializeField] private Toggle negateToggle;
        [SerializeField] private Button sayButton;
        [SerializeField] private Button presentButton;
        [SerializeField] private TMP_InputField presentAgainstStatement; // statement # from the notebook

        private readonly List<(int id, string name)> _subjects = new List<(int, string)>();
        private readonly List<string> _discoveredClues = new List<string>(); // ids, in discovery order
        private readonly List<string> _discoveredNames = new List<string>();
        private int _subject, _claim, _value, _time, _evidence = -1;

        private static readonly Predicate[] Claims =
            { Predicate.WasIn, Predicate.Had, Predicate.Touched, Predicate.Saw, Predicate.WasAsleep };
        private static readonly string[] ClaimText = { "was in", "had", "touched", "saw", "was asleep" };

        private void Start()
        {
            subjectPrev.onClick.AddListener(() => Step(ref _subject, -1, _subjects.Count));
            subjectNext.onClick.AddListener(() => Step(ref _subject, +1, _subjects.Count));
            claimPrev.onClick.AddListener(() => { Step(ref _claim, -1, Claims.Length); _value = 0; });
            claimNext.onClick.AddListener(() => { Step(ref _claim, +1, Claims.Length); _value = 0; });
            valuePrev.onClick.AddListener(() => Step(ref _value, -1, Values().Count));
            valueNext.onClick.AddListener(() => Step(ref _value, +1, Values().Count));
            timePrev.onClick.AddListener(() => Step(ref _time, -1, caseDefinition.TimeSlots.Count));
            timeNext.onClick.AddListener(() => Step(ref _time, +1, caseDefinition.TimeSlots.Count));
            evidenceNext.onClick.AddListener(CycleEvidence);
            negateToggle.onValueChanged.AddListener(_ => Refresh());
            sayButton.onClick.AddListener(Say);
            presentButton.onClick.AddListener(Present);

            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.ClueDiscovered += (id, name, _, __) => { _discoveredClues.Add(id); _discoveredNames.Add(name); };
                MatchDirector.Instance.PhaseChanged += p =>
                    panel.SetActive(p == MatchPhase.OpeningAlibis || p == MatchPhase.Meeting || p == MatchPhase.FinalMeeting);
            }
            panel.SetActive(false);
        }

        private void OnEnable() => Refresh();

        private void RebuildSubjects()
        {
            _subjects.Clear();
            _subjects.Add((ShortLegsPlayer.Local != null ? ShortLegsPlayer.Local.Slot.Value : 0, "I"));
            foreach (var p in ShortLegsPlayer.All)
                if (p != ShortLegsPlayer.Local) _subjects.Add((p.Slot.Value, p.DisplayName.Value.ToString()));
            _subjects.Add((FactClaim.AnySubject, "Nobody"));
            _subject = Mathf.Clamp(_subject, 0, _subjects.Count - 1);
        }

        private List<NamedId> Values() =>
            Claims[_claim] == Predicate.WasIn ? caseDefinition.Rooms :
            Claims[_claim] == Predicate.WasAsleep ? new List<NamedId> { new NamedId { Id = 0, Name = "—" } } :
            caseDefinition.Objects;

        private void Step(ref int index, int delta, int count)
        {
            if (count <= 0) return;
            index = (index + delta + count) % count;
            Refresh();
        }

        private void CycleEvidence()
        {
            _evidence = _discoveredClues.Count == 0 ? -1 : (_evidence + 2) % (_discoveredClues.Count + 1) - 1;
            Refresh();
        }

        private StatementPacket Build()
        {
            RebuildSubjects();
            var (subjectId, _) = _subjects[_subject];
            bool nobody = subjectId == FactClaim.AnySubject;
            var values = Values();
            var claim = new FactClaim(subjectId, Claims[_claim], values[Mathf.Clamp(_value, 0, values.Count - 1)].Id,
                caseDefinition.TimeSlots[Mathf.Clamp(_time, 0, caseDefinition.TimeSlots.Count - 1)],
                negated: nobody || negateToggle.isOn);
            return new StatementPacket(claim, _evidence >= 0 ? _discoveredClues[_evidence] : null);
        }

        private void Refresh()
        {
            if (caseDefinition == null || subjectLabel == null) return;
            RebuildSubjects();
            var values = Values();
            _value = Mathf.Clamp(_value, 0, values.Count - 1);
            bool nobody = _subjects[_subject].id == FactClaim.AnySubject;

            subjectLabel.text = _subjects[_subject].name;
            claimLabel.text = (negateToggle.isOn && !nobody ? "did not… " : "") + ClaimText[_claim];
            valueLabel.text = values[_value].Name;
            timeLabel.text = $"{caseDefinition.TimeSlots[_time]:00}:00";
            evidenceLabel.text = _evidence >= 0 ? _discoveredNames[_evidence] : "(no evidence)";
            previewLabel.text = $"\"{subjectLabel.text} {claimLabel.text} {valueLabel.text} at {timeLabel.text}\"";
        }

        private void Say()
        {
            if (ShortLegsPlayer.Local != null) ShortLegsPlayer.Local.SubmitStatementRpc(Build());
        }

        private void Present()
        {
            if (ShortLegsPlayer.Local == null || _evidence < 0) return;
            if (!int.TryParse(presentAgainstStatement.text.TrimStart('#'), out int index)) return;
            ShortLegsPlayer.Local.PresentEvidenceRpc(index, _discoveredClues[_evidence]);
        }
    }
}
