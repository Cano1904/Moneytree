using System.Collections;
using ShortLegs.Core;
using ShortLegs.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShortLegs.UI
{
    /// <summary>
    /// Main menu (GDD §2): interrogation room, silhouette whose legs shrink on every lie-detector flash,
    /// vertical left-aligned navigation.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button storyButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button archiveButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private Button quitConfirmYes;
        [SerializeField] private Button quitConfirmNo;

        [Header("Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject archivePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private string storyScene = "Story_Case01_MoneyTree";
        [SerializeField] private string lobbyScene = "Lobby";

        [Header("Scene dressing")]
        [SerializeField] private LegRig silhouette;
        [SerializeField] private Light lieDetectorLight;
        [SerializeField] private float flashIntensity = 6f;
        [SerializeField] private Vector2 flashInterval = new Vector2(4f, 7f);
        [SerializeField] private RectTransform titleLowerHalf; // bottom half of the "SHORT LEGS" glyphs
        [SerializeField] private AudioSource stingSource;

        private int _menuLies;

        private void Start()
        {
            storyButton.onClick.AddListener(() => SceneManager.LoadScene(storyScene));
            multiplayerButton.onClick.AddListener(() => SceneManager.LoadScene(lobbyScene));
            archiveButton.onClick.AddListener(() => Show(archivePanel));
            settingsButton.onClick.AddListener(() => Show(settingsPanel));
            quitButton.onClick.AddListener(() => quitConfirmPanel.SetActive(true));
            quitConfirmNo.onClick.AddListener(() => quitConfirmPanel.SetActive(false));
            quitConfirmYes.onClick.AddListener(Quit);

            quitConfirmPanel.SetActive(false);
            Show(rootPanel);
            if (lieDetectorLight != null) lieDetectorLight.intensity = 0f;
            StartCoroutine(LieDetectorLoop());
        }

        public void Show(GameObject panel)
        {
            rootPanel.SetActive(panel == rootPanel);
            if (archivePanel != null) archivePanel.SetActive(panel == archivePanel);
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (panel == rootPanel) EventSystem.current?.SetSelectedGameObject(storyButton.gameObject); // gamepad focus
        }

        public void BackToRoot() => Show(rootPanel);

        private IEnumerator LieDetectorLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(flashInterval.x, flashInterval.y));

                _menuLies = _menuLies >= 3 ? 0 : _menuLies + 1;
                if (_menuLies == 0) silhouette?.SetScaleImmediate(1f);
                else silhouette?.PlayShrink(ShrinkMatrix.LegScale(_menuLies), _menuLies);
                if (stingSource != null) { stingSource.pitch = ShrinkMatrix.VoicePitch(_menuLies); stingSource.Play(); }

                if (titleLowerHalf != null)
                    titleLowerHalf.localScale = new Vector3(1f, Mathf.Max(0.4f, ShrinkMatrix.LegScale(_menuLies)), 1f);

                if (lieDetectorLight == null) continue;
                bool reduce = Settings.SettingsManager.Instance != null && Settings.SettingsManager.Instance.Current.ReduceFlashing;
                float peak = reduce ? flashIntensity * 0.25f : flashIntensity;
                for (float t = 0; t < 0.5f; t += Time.deltaTime)
                {
                    lieDetectorLight.intensity = Mathf.Lerp(peak, 0f, t / 0.5f);
                    yield return null;
                }
                lieDetectorLight.intensity = 0f;
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
