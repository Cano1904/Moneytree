using ShortLegs.Core;
using ShortLegs.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShortLegs.Gameplay
{
    /// <summary>
    /// Owner-side movement that obeys the Shrink Matrix (GDD §5.2): Speed = Base × LegScale,
    /// no sprint at 1 lie, no step-up at 2, no jump + crawl at 3. The server re-checks speed.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(ShortLegsPlayer))]
    public sealed class ShrinkAwareLocomotion : NetworkBehaviour
    {
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference look;
        [SerializeField] private InputActionReference sprint;
        [SerializeField] private InputActionReference jump;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Animator animator;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -19.6f;
        [SerializeField] private float baseStepOffset = 0.35f;
        [SerializeField] private float lookSensitivity = 0.12f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int CrawlHash = Animator.StringToHash("Crawl");
        private static readonly int WaddleHash = Animator.StringToHash("Waddle");

        private CharacterController _cc;
        private ShortLegsPlayer _player;
        private float _verticalVelocity;
        private float _pitch;

        /// <summary>Set by the notebook / pause menu / meetings to freeze the pawn locally.</summary>
        public bool InputBlocked { get; set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _player = GetComponent<ShortLegsPlayer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            if (cameraPivot != null) cameraPivot.gameObject.SetActive(IsOwner);
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            var caps = _player.Caps;
            _cc.stepOffset = caps.CanStepUp ? baseStepOffset : 0f;

            bool blocked = InputBlocked || _player.IsGhost.Value;
            Vector2 input = blocked || move == null ? Vector2.zero : move.action.ReadValue<Vector2>();
            bool wantsSprint = !blocked && sprint != null && sprint.action.IsPressed();
            bool wantsJump = !blocked && jump != null && jump.action.WasPressedThisFrame();

            if (!blocked && look != null) Look(look.action.ReadValue<Vector2>());

            float speed = ShrinkMatrix.Speed(_player.BaseWalkSpeed, _player.LieCount.Value);
            if (wantsSprint && caps.CanSprint) speed *= _player.SprintMultiplier;

            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            if (_cc.isGrounded)
            {
                _verticalVelocity = -2f;
                if (wantsJump && caps.CanJump) _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity) * Mathf.Max(0.5f, caps.LegScale);
            }
            _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move((wish * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, wish.magnitude * speed);
                animator.SetBool(CrawlHash, caps.MustCrawl);
                animator.SetBool(WaddleHash, !caps.CanStepUp && !caps.MustCrawl);
            }
        }

        private void Look(Vector2 delta)
        {
            transform.Rotate(0f, delta.x * lookSensitivity, 0f);
            if (cameraPivot == null) return;
            _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -80f, 80f);
            cameraPivot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // CharacterController overrides transform writes while enabled.
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _verticalVelocity = 0f;
        }
    }
}
