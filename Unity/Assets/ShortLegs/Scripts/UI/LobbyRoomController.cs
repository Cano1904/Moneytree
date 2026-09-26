using ShortLegs.Cases;
using ShortLegs.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ShortLegs.UI
{
    /// <summary>
    /// Waiting-room UI (GDD §3.1): host edits Max Lies / voice / map / timer / meetings; everyone readies up;
    /// host starts when ≥ 4 players are ready.
    /// </summary>
    public sealed class LobbyRoomController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text lobbyCodeText;
        [SerializeField] private TMP_Text playersText;
        [SerializeField] private SessionConnector connector;

        [Header("Host settings")]
        [SerializeField] private Slider maxLiesSlider;        // 1..4
        [SerializeField] private Toggle proximityVoiceToggle;
        [SerializeField] private TMP_Dropdown mapDropdown;    // Grand Manor, Sunken Yacht, Night Train, Frozen Lodge
        [SerializeField] private Slider matchMinutesSlider;   // 8..20
        [SerializeField] private Slider meetingsSlider;       // 1..3
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyLabel;
        [SerializeField] private Button startButton;

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private void Start()
        {
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(StartMatch);
            maxLiesSlider.onValueChanged.AddListener(_ => PushSettings());
            proximityVoiceToggle.onValueChanged.AddListener(_ => PushSettings());
            mapDropdown.onValueChanged.AddListener(_ => PushSettings());
            matchMinutesSlider.onValueChanged.AddListener(_ => PushSettings());
            meetingsSlider.onValueChanged.AddListener(_ => PushSettings());
        }

        private void Update()
        {
            var director = MatchDirector.Instance;
            bool inLobby = director != null && director.Phase.Value == MatchPhase.Lobby;
            panel.SetActive(inLobby);
            if (!inLobby) return;

            lobbyCodeText.text = connector != null && connector.LobbyCode != null ? $"CODE  {connector.LobbyCode}" : "";

            // Non-hosts see the host's settings read-only.
            bool host = IsHost;
            maxLiesSlider.interactable = proximityVoiceToggle.interactable = mapDropdown.interactable =
                matchMinutesSlider.interactable = meetingsSlider.interactable = host;
            if (!host) ShowSettings(director.Settings.Value);

            int ready = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var p in ShortLegsPlayer.All)
            {
                if (p.IsReady.Value) ready++;
                sb.AppendLine($"{(p.IsReady.Value ? "✔" : "…")} {p.DisplayName.Value}");
            }
            playersText.text = $"{ShortLegsPlayer.All.Count}/{LobbySettings.MaxPlayers}\n{sb}";

            var local = ShortLegsPlayer.Local;
            readyLabel.text = local != null && local.IsReady.Value ? "UNREADY" : "READY";
            startButton.gameObject.SetActive(host);
            startButton.interactable = ShortLegsPlayer.All.Count >= LobbySettings.MinPlayers && ready == ShortLegsPlayer.All.Count;
        }

        private void ToggleReady()
        {
            var local = ShortLegsPlayer.Local;
            if (local != null) local.IsReady.Value = !local.IsReady.Value;
        }

        private LobbySettings ReadSettings() => new LobbySettings
        {
            MaxLiesAllowed = Mathf.RoundToInt(maxLiesSlider.value),
            ProximityVoice = proximityVoiceToggle.isOn,
            Map = (MapId)mapDropdown.value,
            MatchMinutes = Mathf.RoundToInt(matchMinutesSlider.value),
            MeetingsPerPlayer = Mathf.RoundToInt(meetingsSlider.value),
        }.Clamped();

        private void ShowSettings(LobbySettings s)
        {
            maxLiesSlider.SetValueWithoutNotify(s.MaxLiesAllowed);
            proximityVoiceToggle.SetIsOnWithoutNotify(s.ProximityVoice);
            mapDropdown.SetValueWithoutNotify((int)s.Map);
            matchMinutesSlider.SetValueWithoutNotify(s.MatchMinutes);
            meetingsSlider.SetValueWithoutNotify(s.MeetingsPerPlayer);
        }

        private void PushSettings()
        {
            if (!IsHost || MatchDirector.Instance == null) return;
            var previous = MatchDirector.Instance.Settings.Value;
            var next = ReadSettings();
            MatchDirector.Instance.Settings.Value = next;
            if (next.Map != previous.Map && connector != null) connector.LoadMap(next.Map);
        }

        private void StartMatch()
        {
            if (IsHost && MatchDirector.Instance != null) MatchDirector.Instance.ServerStartMatch(ReadSettings());
        }
    }
}
