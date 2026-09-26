using System.Text;
using ShortLegs.Core;
using ShortLegs.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShortLegs.UI
{
    /// <summary>
    /// Detective Notebook (Tab / View): clues, statements, player profiles and the Leg Status Tracker.
    /// D-Pad Up/Down scrolls.
    /// </summary>
    public sealed class NotebookController : MonoBehaviour
    {
        [SerializeField] private InputActionReference toggleAction;
        [SerializeField] private InputActionReference scrollAction;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private TMP_Text cluesText;
        [SerializeField] private TMP_Text statementsText;
        [SerializeField] private TMP_Text legTrackerText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private float scrollSpeed = 1.2f;

        private readonly StringBuilder _clues = new StringBuilder();
        private readonly StringBuilder _statements = new StringBuilder();
        private PauseMenuController _returnTo;

        private void OnEnable()
        {
            if (toggleAction != null) toggleAction.action.performed += OnToggle;
            ShortLegsPlayer.AnyShrinkEvent += OnShrink;
            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.ClueDiscovered += OnClue;
                MatchDirector.Instance.StatementLogged += OnStatement;
            }
        }

        private void OnDisable()
        {
            if (toggleAction != null) toggleAction.action.performed -= OnToggle;
            ShortLegsPlayer.AnyShrinkEvent -= OnShrink;
            if (MatchDirector.Instance != null)
            {
                MatchDirector.Instance.ClueDiscovered -= OnClue;
                MatchDirector.Instance.StatementLogged -= OnStatement;
            }
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (panel.activeSelf) Close(); else Open();
        }

        public void Open(PauseMenuController returnTo = null)
        {
            _returnTo = returnTo;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            if (_returnTo != null) { _returnTo.ShowMain(); _returnTo = null; }
        }

        private void Update()
        {
            if (!panel.activeSelf) return;
            if (scroll != null && scrollAction != null)
            {
                float v = scrollAction.action.ReadValue<Vector2>().y;
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + v * scrollSpeed * Time.unscaledDeltaTime);
            }
            RefreshLegTracker();
        }

        private void OnClue(string id, string name, string description, int finder)
        {
            _clues.AppendLine($"<b>{name}</b> — found by {NameOf(finder)}\n<size=80%>{description}</size>");
            Refresh();
        }

        private void OnStatement(int index, int speaker, StatementPacket p)
        {
            string subject = p.SubjectId == FactClaim.AnySubject ? (p.Negated ? "Nobody" : "Somebody") : NameOf(p.SubjectId);
            _statements.AppendLine($"#{index} {NameOf(speaker)}: \"{subject} {(p.Negated && p.SubjectId != FactClaim.AnySubject ? "did not " : "")}{p.Predicate} {p.ValueId} @ {p.TimeSlot}:00\"");
            Refresh();
        }

        private void OnShrink(ShrinkEventInfo e)
        {
            _statements.AppendLine($"<color=#FF2D55>▼ {NameOf(e.Slot)} shrank to {e.LegScale:P0} — {e.Reason}</color>");
            Refresh();
        }

        private void Refresh()
        {
            if (cluesText != null) cluesText.text = _clues.Length == 0 ? "<i>No clues yet.</i>" : _clues.ToString();
            if (statementsText != null) statementsText.text = _statements.ToString();
            if (roleText != null && ShortLegsPlayer.Local != null)
                roleText.text = ShortLegsPlayer.Local.LocalRole == PlayerRole.Liar ? "<color=#FF2D55>THE LIAR</color>" : "INVESTIGATOR";
            RefreshLegTracker();
        }

        /// <summary>Leg Status Tracker: one bar per player, public to everyone.</summary>
        private void RefreshLegTracker()
        {
            if (legTrackerText == null) return;
            var sb = new StringBuilder();
            foreach (var p in ShortLegsPlayer.All)
            {
                int lies = p.LieCount.Value;
                int filled = Mathf.RoundToInt(ShrinkMatrix.LegScale(lies) * 4);
                string bar = new string('█', filled) + new string('░', 4 - filled);
                var caps = p.Caps;
                string status = caps.MustCrawl ? "crawling" : !caps.CanStepUp ? "no stairs" : !caps.CanSprint ? "no sprint" : "fine";
                sb.AppendLine($"{p.DisplayName.Value}{(p.IsGhost.Value ? " (ghost)" : "")}  {bar}  {lies} lie(s) · {status}");
            }
            legTrackerText.text = sb.ToString();
        }

        private static string NameOf(int slot)
        {
            foreach (var p in ShortLegsPlayer.All) if (p.Slot.Value == slot) return p.DisplayName.Value.ToString();
            return slot >= 100 ? $"NPC {slot}" : $"Player {slot + 1}";
        }
    }
}
