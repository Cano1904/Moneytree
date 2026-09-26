using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShortLegs.Networking
{
    /// <summary>
    /// [MULTIPLAYER LOBBY] entry: host a session (6-char lobby code via Unity Relay), join by code, or quick match.
    /// The Multiplayer Services SDK starts the NGO host/client for us once the Relay network is up.
    /// </summary>
    public sealed class SessionConnector : MonoBehaviour
    {
        [SerializeField] private string[] mapScenes = { "Map_GrandManor", "Map_SunkenYacht", "Map_NightTrain", "Map_FrozenLodge" };

        public ISession Session { get; private set; }
        public string LobbyCode => Session?.Code;
        public event Action<string> StatusChanged;

        private async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static SessionOptions Options() =>
            new SessionOptions { MaxPlayers = LobbySettings.MaxPlayers, IsPrivate = false }.WithRelayNetwork();

        public async void Host(Cases.MapId map)
        {
            try
            {
                StatusChanged?.Invoke("Opening the case file…");
                await EnsureSignedIn();
                Session = await MultiplayerService.Instance.CreateSessionAsync(Options());
                StatusChanged?.Invoke($"Lobby code: {Session.Code}");
                // The host loads the map; its entrance hall is the waiting room (MatchPhase.Lobby).
                NetworkManager.Singleton.SceneManager.LoadScene(mapScenes[(int)map], LoadSceneMode.Single);
            }
            catch (Exception e) { Fail(e); }
        }

        public async void JoinByCode(string code)
        {
            try
            {
                code = (code ?? string.Empty).Trim().ToUpperInvariant();
                if (code.Length != 6) { StatusChanged?.Invoke("Lobby codes have 6 characters."); return; }
                StatusChanged?.Invoke("Knocking on the door…");
                await EnsureSignedIn();
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                StatusChanged?.Invoke("Joined. Waiting for the host…"); // NGO scene sync moves us into the map
            }
            catch (Exception e) { Fail(e); }
        }

        public async void QuickMatch()
        {
            try
            {
                StatusChanged?.Invoke("Looking for a crime scene…");
                await EnsureSignedIn();
                Session = await MultiplayerService.Instance.MatchmakeSessionAsync(new QuickJoinOptions(), Options());
                if (Session.IsHost) NetworkManager.Singleton.SceneManager.LoadScene(mapScenes[0], LoadSceneMode.Single);
            }
            catch (Exception e) { Fail(e); }
        }

        /// <summary>Host changed the crime scene in the waiting room: everyone follows via NGO scene sync.</summary>
        public void LoadMap(Cases.MapId map)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                NetworkManager.Singleton.SceneManager.LoadScene(mapScenes[(int)map], LoadSceneMode.Single);
        }

        public async void Leave()
        {
            try { if (Session != null) await Session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning(e); }
            Session = null;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }

        private void Fail(Exception e)
        {
            Debug.LogException(e);
            StatusChanged?.Invoke("Connection failed. Try again.");
        }
    }
}
