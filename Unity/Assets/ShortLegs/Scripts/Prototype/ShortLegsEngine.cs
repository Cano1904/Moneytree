using ShortLegs.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace ShortLegs.Prototype
{
    /// <summary>
    /// Repräsentiert die Rollen im Multiplayer-Modus.
    /// </summary>
    public enum PlayerRole { Investigator, Liar }

    /// <summary>
    /// Repräsentiert den aktuellen Zustand der Beinschrumpfung.
    /// </summary>
    public enum ShrinkStage { Normal = 0, OneLie = 1, TwoLies = 2, ThreeLies = 3, Caught = 4 }

    /// <summary>
    /// Offline-Prototyp der Shrink Matrix (Kapitel 5.2) auf einem einzelnen Objekt – zum schnellen Testen
    /// ohne Netzwerk. Die Werte kommen aus <see cref="ShrinkMatrix"/>, damit Prototyp und Multiplayer
    /// (<c>ShortLegsPlayer</c> + <c>LegRig</c> + <c>ShrinkAwareLocomotion</c>) identisch rechnen.
    /// Szene automatisch anlegen: Tools ▸ Short Legs ▸ Create Test Scene.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class ShortLegsEngine : MonoBehaviour
    {
        [Header("--- 1. NETWORK & IDENTITY ---")]
        [SerializeField] private string playerId = "Detective";
        [SerializeField] private bool isLocalPlayer = true;
        [SerializeField] private PlayerRole playerRole = PlayerRole.Investigator;

        [Header("--- 5.2 THE SHRINK MATRIX PARAMETERS ---")]
        [SerializeField] private Transform leftLegBone;
        [SerializeField] private Transform rightLegBone;
        [SerializeField] private float baseMovementSpeed = 6.0f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float baseJumpHeight = 2.0f;
        [Tooltip("Lobby-Einstellung \"Max Lies Allowed\" (1–4).")]
        [SerializeField, Range(1, 4)] private int maxLiesAllowed = 4;
        [Tooltip("Optional: Modell-Wurzel, die beim Schrumpfen abgesenkt wird, damit die Füße am Boden bleiben.")]
        [SerializeField] private Transform modelRoot;
        [Tooltip("Beinlänge (Hüfte bis Boden) bei Skalierung 1 – nur zusammen mit modelRoot genutzt.")]
        [SerializeField] private float legLength = 1.0f;
        [Tooltip("Visuelles Minimum, damit die Knochen nie auf exakt 0 skaliert werden.")]
        [SerializeField] private float minimumVisualLegScale = 0.1f;
        [Tooltip("Collider-Höhe im Krabbelmodus (ab 3 Lügen).")]
        [SerializeField] private float crawlHeight = 0.6f;

        [Header("Audio DSP Settings")]
        [SerializeField] private AudioMixerGroup voiceAudioMixerGroup;
        [Tooltip("Exponierter Parameter eines Pitch Shifters im Mixer (optional).")]
        [SerializeField] private string mixerPitchParameter = "VoicePitchParam";

        [Header("Debug")]
        [Tooltip("Taste zum Testen: simuliert eine erkannte Lüge.")]
        [SerializeField] private Key debugLieKey = Key.L;
        [SerializeField] private Key debugResetKey = Key.R;

        // Interne Systemvariablen
        private CharacterController characterController;
        private AudioSource voiceAudioSource;

        private int currentLieCount = 0;
        private float currentLegScale = 1.0f;
        private float activeMovementSpeed;
        private float activeJumpHeight;
        private Vector3 playerVelocity;
        private bool isGrounded;
        private bool isDefeated;
        private float gravityValue = -9.81f;

        // Ausgangswerte des CharacterControllers, damit Stufe 2/3 wieder zurückgesetzt werden können
        private float baseStepOffset;
        private float baseHeight;
        private Vector3 baseCenter;
        private Vector3 leftLegBaseScale = Vector3.one;
        private Vector3 rightLegBaseScale = Vector3.one;
        private float modelRootBaseY;

        // Getters für das UI-Notebook (Kapitel 6.1 - TAB)
        public int CurrentLieCount => currentLieCount;
        public float CurrentLegScale => currentLegScale;
        public PlayerRole PlayerRole => playerRole;
        public ShrinkStage Stage => (ShrinkStage)Mathf.Min(currentLieCount, (int)ShrinkStage.Caught);
        public bool IsDefeated => isDefeated;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            voiceAudioSource = GetComponent<AudioSource>();

            baseStepOffset = characterController.stepOffset;
            baseHeight = characterController.height;
            baseCenter = characterController.center;
            if (leftLegBone != null) leftLegBaseScale = leftLegBone.localScale;
            if (rightLegBone != null) rightLegBaseScale = rightLegBone.localScale;
            if (modelRoot != null) modelRootBaseY = modelRoot.localPosition.y;

            if (voiceAudioMixerGroup != null)
            {
                voiceAudioSource.outputAudioMixerGroup = voiceAudioMixerGroup;
            }
        }

        private void Start()
        {
            InitializePlayer();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[debugLieKey].wasPressedThisFrame) OnDeceptionDetected();
            if (keyboard != null && keyboard[debugResetKey].wasPressedThisFrame) InitializePlayer();

            // Ground-Check für die Physik
            isGrounded = characterController.isGrounded;
            if (isGrounded && playerVelocity.y < 0)
            {
                playerVelocity.y = -2f; // leicht an den Boden drücken, damit isGrounded stabil bleibt
            }

            HandleLocomotion();
        }

        /// <summary>
        /// Initialisiert die Startparameter basierend auf der Engine-Spezifikation.
        /// </summary>
        public void InitializePlayer()
        {
            isDefeated = false;
            RPC_SyncBoneScale(0);
        }

        /// <summary>
        /// SCRIPT-INTERFACE: Simuliert den Empfang eines Server-RPCs zur Synchronisation der Knochen.
        /// </summary>
        public void RPC_SyncBoneScale(int newLieCount)
        {
            currentLieCount = Mathf.Clamp(newLieCount, 0, (int)ShrinkStage.Caught);

            // Berechnung der Schrumpfung: -25% pro Lüge (Kapitel 5.2)
            currentLegScale = ShrinkMatrix.LegScale(currentLieCount);

            // Berechne neue Gameplay-Einschränkungen (Linearer Verfall)
            activeMovementSpeed = ShrinkMatrix.Speed(baseMovementSpeed, currentLieCount);
            activeJumpHeight = ShrinkMatrix.CanJump(currentLieCount) ? baseJumpHeight : 0f;

            UpdateBoneTransforms();
            ApplyAudioPitchShift();
            EvaluateNavigationObstacles();
        }

        /// <summary>
        /// Wendet die skelettförmige Transformation direkt auf die Beinknochen an.
        /// </summary>
        private void UpdateBoneTransforms()
        {
            if (leftLegBone != null && rightLegBone != null)
            {
                // Skaliere nur die Y-Achse (Länge der Beine), X/Z des Rigs bleiben erhalten
                float visual = Mathf.Max(currentLegScale, minimumVisualLegScale);
                leftLegBone.localScale = new Vector3(leftLegBaseScale.x, leftLegBaseScale.y * visual, leftLegBaseScale.z);
                rightLegBone.localScale = new Vector3(rightLegBaseScale.x, rightLegBaseScale.y * visual, rightLegBaseScale.z);

                // Beine schrumpfen zur Hüfte hin – das Modell sinkt um dieselbe Strecke, Füße bleiben am Boden
                if (modelRoot != null)
                {
                    Vector3 p = modelRoot.localPosition;
                    p.y = modelRootBaseY - legLength * (1f - visual);
                    modelRoot.localPosition = p;
                }
            }
            else
            {
                Debug.LogWarning("[SHORT LEGS ENGINE] Warnung: Beinknochen nicht im Inspector zugewiesen!");
            }
        }

        /// <summary>
        /// Berechnet die physikalischen Barrieren basierend auf der Stufe der Lüge (Kapitel 5.2).
        /// Jede Stufe setzt den Controller vollständig, damit auch ein Zurücksetzen korrekt funktioniert.
        /// </summary>
        private void EvaluateNavigationObstacles()
        {
            // Stufe 2+: Treppen und Kanten sind nicht mehr überwindbar
            characterController.stepOffset = ShrinkMatrix.CanStepUp(currentLieCount) ? baseStepOffset : 0f;

            // Stufe 3+: Krabbelmodus – Collider nach unten skalieren, Füße bleiben am Boden
            float height = ShrinkMatrix.MustCrawl(currentLieCount) ? Mathf.Min(crawlHeight, baseHeight) : baseHeight;
            characterController.height = height;
            characterController.center = new Vector3(baseCenter.x, baseCenter.y - (baseHeight - height) * 0.5f, baseCenter.z);

            if (currentLieCount >= maxLiesAllowed)
            {
                Debug.Log($"[{playerId}] Stufe {currentLieCount}: Lügen haben keine Beine mehr! INSTANT DEFEAT.");
                TriggerInstantDefeat();
                return;
            }

            switch (Stage)
            {
                case ShrinkStage.OneLie:
                    Debug.Log($"[{playerId}] Stufe 1: Sprinten deaktiviert.");
                    break;
                case ShrinkStage.TwoLies:
                    Debug.Log($"[{playerId}] Stufe 2: Stufen- und Kanten-Navigation blockiert.");
                    break;
                case ShrinkStage.ThreeLies:
                    Debug.Log($"[{playerId}] Stufe 3: Springen blockiert. Spieler muss krabbeln.");
                    break;
            }
        }

        /// <summary>
        /// Modifiziert die Stimmfrequenz via Pitch-Shifter DSP um +15% pro Lüge (Kapitel 5.2).
        /// </summary>
        private void ApplyAudioPitchShift()
        {
            float targetPitch = ShrinkMatrix.VoicePitch(currentLieCount);

            // Direkte Zuweisung an die AudioSource – für lokale Clips/Stimmen im Prototyp.
            // Live-VOIP im Multiplayer läuft stattdessen über VoicePitchShifter (Mixer-Gruppen).
            voiceAudioSource.pitch = targetPitch;

            if (voiceAudioMixerGroup != null && voiceAudioMixerGroup.audioMixer != null && !string.IsNullOrEmpty(mixerPitchParameter))
            {
                // Falls ein hochpräziser Pitch-Shifter im Mixer genutzt wird (Parameter muss exponiert sein):
                voiceAudioMixerGroup.audioMixer.SetFloat(mixerPitchParameter, targetPitch);
            }
        }

        /// <summary>
        /// Verarbeitet die Bewegung basierend auf den verbleibenden Beinlängen-Faktoren (Kapitel 6.1 / 6.2).
        /// Nutzt das neue Input System (WASD / Left Stick, Shift / L3 Sprint, Space / Süd-Taste Sprung).
        /// </summary>
        private void HandleLocomotion()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            }
            if (gamepad != null && gamepad.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
            {
                input = gamepad.leftStick.ReadValue();
            }

            bool sprintPressed = (keyboard != null && keyboard.leftShiftKey.isPressed) ||
                                 (gamepad != null && gamepad.leftStickButton.isPressed);
            bool jumpPressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                               (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            // Geschwindigkeit ist durch Beinskalierung gemindert; Sprinten nur ohne Lüge
            float speed = isDefeated ? 0f : activeMovementSpeed;
            if (sprintPressed && ShrinkMatrix.CanSprint(currentLieCount)) speed *= sprintMultiplier;

            // Sprung-Logik (Nur erlaubt, wenn < 3 Lügen)
            if (jumpPressed && isGrounded && !isDefeated && activeJumpHeight > 0f)
            {
                playerVelocity.y = Mathf.Sqrt(activeJumpHeight * -2.0f * gravityValue);
            }

            // Schwerkraft berechnen – Bewegung und Fall in einem Move-Aufruf
            playerVelocity.y += gravityValue * Time.deltaTime;
            Vector3 motion = moveDirection * speed + Vector3.up * playerVelocity.y;
            characterController.Move(motion * Time.deltaTime);
        }

        /// <summary>
        /// Wird aufgerufen, wenn die "Max Lies Allowed" Grenze erreicht ist.
        /// </summary>
        private void TriggerInstantDefeat()
        {
            isDefeated = true;
            activeMovementSpeed = 0f;
            activeJumpHeight = 0f;

            if (isLocalPlayer)
            {
                // Hier das Pausen/Endmenü aufrufen
                Debug.LogWarning("GAME OVER: Du bist sprichwörtlich über deine eigenen Lügen gestolpert!");
            }
        }

        /// <summary>
        /// Event-Trigger, der aufgerufen wird, wenn die "Evidence Binding Wheel" Auswertung fehlschlägt.
        /// </summary>
        public void OnDeceptionDetected()
        {
            if (isDefeated) return;
            int nextLieStage = currentLieCount + 1;
            Debug.LogWarning($"[SERVER DETECTED LIE] Spieler {playerId} hat gelogen! Setze neue Stufe: {nextLieStage}");

            // Server sendet RPC an alle Clients (hier lokal simuliert)
            RPC_SyncBoneScale(nextLieStage);
        }
    }
}
