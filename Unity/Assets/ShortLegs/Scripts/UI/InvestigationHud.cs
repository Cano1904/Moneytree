using ShortLegs.Networking;
using ShortLegs.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortLegs.UI
{
    /// <summary>Timer, phase banner, clue counter, lie-detector flash and verdict/subtitle line.</summary>
    public sealed class InvestigationHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text cluesText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image subtitleBackground;
        [SerializeField] private Image lieFlash;         // full-screen red overlay
        [SerializeField] private Image reducedFlashBorder;
        [SerializeField] private GameObject roleCard;
        [SerializeField] private TMP_Text roleCardText;
        [SerializeField] private float subtitleSeconds = 4f;

        private static readonly Color LieRed = new Color(1f, 0.176f, 0.333f);
        private float _flash;
        private float _subtitleUntil;

        private void OnEnable()
        {
            ShortLegsPlayer.AnyShrinkEvent += OnShrink;
            if (MatchDirector.Instance != null) MatchDirector.Instance.VerdictAnnounced += OnVerdict;
        }

        private void OnDisable()
        {
            ShortLegsPlayer.AnyShrinkEvent -= OnShrink;
            if (MatchDirector.Instance != null) MatchDirector.Instance.VerdictAnnounced -= OnVerdict;
        }

        private void Start()
        {
            if (ShortLegsPlayer.Local != null) ShortLegsPlayer.Local.RoleRevealed += ShowRole;
        }

        private void ShowRole(PlayerRole role)
        {
            roleCard.SetActive(true);
            roleCardText.text = role == PlayerRole.Liar ? "<color=#FF2D55>THE LIAR</color>" : "INVESTIGATOR";
            Invoke(nameof(HideRole), 4.5f);
        }

        private void HideRole() => roleCard.SetActive(false);

        private void OnShrink(ShrinkEventInfo e)
        {
            _flash = 1f;
            string who = NameOf(e.Slot);
            Say($"<color=#FF2D55>LIE DETECTED</color> — {who}'s legs shrink to {e.LegScale:P0}. {e.Reason}");
        }

        private void OnVerdict(VerdictKind kind, int target)
        {
            string who = target >= 0 ? NameOf(target) : "";
            Say(kind switch
            {
                VerdictKind.LiarCaught => $"{who} was the Liar! Case closed.",
                VerdictKind.InsufficientEvidence => $"{who} walks free — not enough evidence (3 clues needed).",
                VerdictKind.InvestigatorFramed => $"{who} was innocent… framed and out of the game.",
                _ => "No majority. The investigation continues.",
            });
        }

        private void Say(string line)
        {
            var s = SettingsManager.Instance != null ? SettingsManager.Instance.Current : null;
            if (s != null && !s.Subtitles) return;
            subtitleText.text = line;
            subtitleText.fontSize = s == null ? 36 : 28 + 8 * (int)s.SubtitleSize;
            if (subtitleBackground != null) subtitleBackground.color = new Color(0, 0, 0, s == null || s.HighContrastSubtitles ? 0.85f : 0.4f);
            _subtitleUntil = Time.unscaledTime + subtitleSeconds;
        }

        private void Update()
        {
            var d = MatchDirector.Instance;
            if (d != null)
            {
                float left = d.SecondsLeftInMatch;
                timerText.text = $"{(int)left / 60:00}:{(int)left % 60:00}";
                phaseText.text = d.Phase.Value switch
                {
                    MatchPhase.OpeningAlibis => "OPENING ALIBIS",
                    MatchPhase.Meeting => "MEETING",
                    MatchPhase.FinalMeeting => "FINAL MEETING",
                    MatchPhase.Accusation => $"ACCUSE! {d.SecondsLeftInPhase:0}s",
                    MatchPhase.Reveal => "CASE CLOSED",
                    _ => d.LightsOut ? "LIGHTS OUT" : "INVESTIGATE",
                };
                cluesText.text = $"CLUES {d.TrueCluesFound.Value}/{LobbySettings.CluesRequired}";
            }

            bool reduce = SettingsManager.Instance != null && SettingsManager.Instance.Current.ReduceFlashing;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 2f);
            if (lieFlash != null) lieFlash.color = new Color(LieRed.r, LieRed.g, LieRed.b, reduce ? 0f : _flash * 0.45f);
            if (reducedFlashBorder != null)
                reducedFlashBorder.color = new Color(LieRed.r, LieRed.g, LieRed.b, reduce ? Mathf.PingPong(_flash * 2f, 1f) : 0f);

            subtitleText.gameObject.SetActive(Time.unscaledTime < _subtitleUntil);
        }

        private static string NameOf(int slot)
        {
            foreach (var p in ShortLegsPlayer.All) if (p.Slot.Value == slot) return p.DisplayName.Value.ToString();
            return $"Player {slot + 1}";
        }
    }
}
