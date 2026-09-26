using ShortLegs.Gameplay;
using ShortLegs.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShortLegs.UI
{
    /// <summary>
    /// Pause menu (GDD §7). Singleplayer: Time.timeScale = 0. Multiplayer: server time keeps running;
    /// only the local pawn is frozen and a "paused" badge shows above the player's head.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private InputActionReference pauseAction;
        [SerializeField] private GameObject panel;            // header: "INVESTIGATION PAUSED"
        [SerializeField] private Volume vignetteVolume;       // dark vignette (0.55) + blur
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button notebookButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button abandonButton;
        [SerializeField] private GameObject abandonConfirm;
        [SerializeField] private Button abandonYes;
        [SerializeField] private Button abandonNo;
        [SerializeField] private NotebookController notebook;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private string mainMenuScene = "MainMenu";

        public bool IsPaused { get; private set; }

        private static bool IsMultiplayer =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && MatchDirector.Instance != null;

        private void Start()
        {
            resumeButton.onClick.AddListener(Resume);
            notebookButton.onClick.AddListener(() => { panel.SetActive(false); notebook.Open(returnTo: this); });
            optionsButton.onClick.AddListener(() => { panel.SetActive(false); optionsPanel.SetActive(true); });
            abandonButton.onClick.AddListener(() => abandonConfirm.SetActive(true));
            abandonNo.onClick.AddListener(() => abandonConfirm.SetActive(false));
            abandonYes.onClick.AddListener(Abandon);
            SetVisible(false);
        }

        private void OnEnable() { if (pauseAction != null) pauseAction.action.performed += OnPausePressed; }
        private void OnDisable() { if (pauseAction != null) pauseAction.action.performed -= OnPausePressed; }

        private void OnPausePressed(InputAction.CallbackContext _)
        {
            if (!IsPaused) Pause();
            else if (optionsPanel.activeSelf || abandonConfirm.activeSelf) ShowMain();
            else Resume();
        }

        public void Pause()
        {
            IsPaused = true;
            if (!IsMultiplayer) Time.timeScale = 0f;
            SetLocalPawnBlocked(true);
            SetVisible(true);
        }

        public void Resume()
        {
            IsPaused = false;
            if (!IsMultiplayer) Time.timeScale = 1f;
            SetLocalPawnBlocked(false);
            SetVisible(false);
        }

        public void ShowMain()
        {
            optionsPanel.SetActive(false);
            abandonConfirm.SetActive(false);
            panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
        }

        private void SetVisible(bool visible)
        {
            panel.SetActive(visible);
            optionsPanel.SetActive(false);
            abandonConfirm.SetActive(false);
            if (vignetteVolume != null) vignetteVolume.weight = visible ? 1f : 0f;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
            if (visible) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
        }

        private static void SetLocalPawnBlocked(bool blocked)
        {
            var local = ShortLegsPlayer.Local;
            if (local == null) return;
            if (local.TryGetComponent<ShrinkAwareLocomotion>(out var loco)) loco.InputBlocked = blocked;
            if (local.IsSpawned) local.SetPausedRpc(blocked);
        }

        private void Abandon()
        {
            Time.timeScale = 1f;
            // In MP this is a disconnect; if the Liar abandons, the server awards the Investigators.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
