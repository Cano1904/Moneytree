using System;
using Glasscore.Simulation;
using UnityEngine;

namespace Glasscore.Client
{
    /// <summary>
    /// Keyboard/mouse + gamepad input on the legacy Input Manager (no package dependency). Key bindings
    /// are plain KeyCodes from <see cref="GameSettings"/> so the remapping matrix can rebind anything.
    /// Gamepad sticks/triggers use the GC_* axes defined in ProjectSettings/InputManager.asset.
    /// </summary>
    public sealed class InputService
    {
        private const string LeftX = "GC_LeftX", LeftY = "GC_LeftY", RightX = "GC_RightX", RightY = "GC_RightY";
        private const string TriggerL = "GC_LT", TriggerR = "GC_RT", DPadY = "GC_DPadY";
        private const float TriggerThreshold = 0.35f;
        private const float StickDeadZone = 0.18f;

        private readonly GameSettings _settings;
        private InputButtons _latched;
        private bool _axesAvailable = true;
        private bool _lastDpadDown;
        private bool _mutePressedThisFrame;
        private float _navRepeat;
        private int _lastNav;

        public float Yaw;
        public float Pitch;
        public bool GameplayEnabled;
        public bool UsingGamepad { get; private set; }

        public InputService(GameSettings settings) { _settings = settings; }

        private bool Key(GameAction a) => _settings.Keyboard[(int)a] != KeyCode.None && Input.GetKey(_settings.Keyboard[(int)a]);
        private bool KeyDown(GameAction a) => _settings.Keyboard[(int)a] != KeyCode.None && Input.GetKeyDown(_settings.Keyboard[(int)a]);
        private bool Pad(GameAction a) => _settings.Gamepad[(int)a] != KeyCode.None && Input.GetKey(_settings.Gamepad[(int)a]);
        private bool PadDown(GameAction a) => _settings.Gamepad[(int)a] != KeyCode.None && Input.GetKeyDown(_settings.Gamepad[(int)a]);

        private float Axis(string name)
        {
            if (!_axesAvailable) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch (ArgumentException)
            {
                _axesAvailable = false; // InputManager.asset missing the GC_* axes: keyboard/mouse only
                Debug.LogWarning("[Input] Gamepad axes not configured; gamepad sticks disabled.");
                return 0f;
            }
        }

        private static float DeadZone(float v) => Mathf.Abs(v) < StickDeadZone ? 0f : Mathf.Sign(v) * (Mathf.Abs(v) - StickDeadZone) / (1f - StickDeadZone);

        public bool FireHeld => Key(GameAction.Fire) || Pad(GameAction.Fire) || (_settings.Gamepad[(int)GameAction.Fire] == KeyCode.None && Axis(TriggerR) > TriggerThreshold);
        public bool AnchorHeld => Key(GameAction.Anchor) || Pad(GameAction.Anchor) || (_settings.Gamepad[(int)GameAction.Anchor] == KeyCode.None && Axis(TriggerL) > TriggerThreshold);
        public bool PushToTalkHeld => Key(GameAction.PushToTalk) || Pad(GameAction.PushToTalk);
        public bool PausePressed => KeyDown(GameAction.Pause) || PadDown(GameAction.Pause);
        public bool MuteTogglePressed => _mutePressedThisFrame;

        /// <summary>Call once per rendered frame before the client ticks.</summary>
        public void Update(float dt)
        {
            UpdateMenuInput(dt);
            bool dpadDown = Axis(DPadY) < -0.5f;
            _mutePressedThisFrame = KeyDown(GameAction.MuteMic) || PadDown(GameAction.MuteMic) ||
                (_settings.Gamepad[(int)GameAction.MuteMic] == KeyCode.None && dpadDown && !_lastDpadDown);
            _lastDpadDown = dpadDown;

            if (!GameplayEnabled)
            {
                _latched = InputButtons.None;
                return;
            }

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            float rx = DeadZone(Axis(RightX));
            float ry = DeadZone(Axis(RightY));
            if (Mathf.Abs(rx) + Mathf.Abs(ry) > 0.01f) UsingGamepad = true;
            else if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.01f) UsingGamepad = false;

            float invert = _settings.InvertY ? -1f : 1f;
            Yaw += mx * _settings.MouseSensitivity + rx * _settings.GamepadLookSpeed * dt;
            Pitch -= (my * _settings.MouseSensitivity + ry * _settings.GamepadLookSpeed * dt) * invert; // GC_RightY: up = +1
            Pitch = Mathf.Clamp(Pitch, -89f, 89f);
            Yaw = Mathf.Repeat(Yaw + 180f, 360f) - 180f;

            // Latch short taps so a press between two 60 Hz ticks is never lost.
            if (KeyDown(GameAction.Jump) || PadDown(GameAction.Jump)) _latched |= InputButtons.Jump;
            if (KeyDown(GameAction.NextWeapon) || PadDown(GameAction.NextWeapon)) _latched |= InputButtons.NextWeapon;
            if (KeyDown(GameAction.PrevWeapon) || PadDown(GameAction.PrevWeapon)) _latched |= InputButtons.PrevWeapon;
            if (KeyDown(GameAction.Anchor) || PadDown(GameAction.Anchor)) _latched |= InputButtons.Anchor;
            if (KeyDown(GameAction.Fire) || PadDown(GameAction.Fire)) _latched |= InputButtons.Fire;
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (scroll > 0.01f) _latched |= InputButtons.NextWeapon;
            else if (scroll < -0.01f) _latched |= InputButtons.PrevWeapon;
        }

        /// <summary>Builds the input for one simulation tick (called by GameClient.InputProvider).</summary>
        public PlayerInput Sample(uint sequence)
        {
            var input = new PlayerInput { Sequence = sequence, Yaw = Yaw, Pitch = Pitch };
            if (!GameplayEnabled) return input; // pause menu disconnects inputs, the match keeps running

            float x = 0f, y = 0f;
            if (Key(GameAction.MoveRight)) x += 1f;
            if (Key(GameAction.MoveLeft)) x -= 1f;
            if (Key(GameAction.MoveForward)) y += 1f;
            if (Key(GameAction.MoveBack)) y -= 1f;
            x += DeadZone(Axis(LeftX));
            y += DeadZone(Axis(LeftY));
            input.MoveX = Mathf.Clamp(x, -1f, 1f);
            input.MoveY = Mathf.Clamp(y, -1f, 1f);

            InputButtons held = InputButtons.None;
            if (Key(GameAction.Jump) || Pad(GameAction.Jump)) held |= InputButtons.Jump;
            if (FireHeld) held |= InputButtons.Fire;
            if (AnchorHeld) held |= InputButtons.Anchor;
            if (Key(GameAction.NextWeapon) || Pad(GameAction.NextWeapon)) held |= InputButtons.NextWeapon;
            if (Key(GameAction.PrevWeapon) || Pad(GameAction.PrevWeapon)) held |= InputButtons.PrevWeapon;

            input.Buttons = held | _latched;
            _latched = InputButtons.None;
            return input;
        }

        // ── menu input: computed once per frame in Update, consumed once (OnGUI runs several times a frame)
        private int _navThisFrame;
        private bool _confirmThisFrame, _backThisFrame;
        private readonly System.Collections.Generic.HashSet<KeyCode> _padThisFrame = new System.Collections.Generic.HashSet<KeyCode>();

        private void UpdateMenuInput(float dt)
        {
            _navThisFrame = ComputeNavigate(dt);
            _confirmThisFrame = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0);
            _backThisFrame = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
            _padThisFrame.Clear();
            for (int i = 0; i < 10; i++)
            {
                var k = (KeyCode)((int)KeyCode.JoystickButton0 + i);
                if (Input.GetKeyDown(k)) _padThisFrame.Add(k);
            }
        }

        private int ComputeNavigate(float dt)
        {
            int dir = 0;
            float stick = Axis(LeftY) + Axis(DPadY);
            if (Input.GetKey(KeyCode.UpArrow) || stick > 0.5f) dir = -1;
            else if (Input.GetKey(KeyCode.DownArrow) || stick < -0.5f) dir = 1;

            if (dir == 0) { _lastNav = 0; _navRepeat = 0f; return 0; }
            if (dir != _lastNav) { _lastNav = dir; _navRepeat = 0.35f; return dir; }
            _navRepeat -= dt;
            if (_navRepeat <= 0f) { _navRepeat = 0.12f; return dir; }
            return 0;
        }

        /// <summary>Vertical menu navigation this frame: -1 (up), +1 (down) or 0. Consumed on read.</summary>
        public int MenuNavigate()
        {
            int v = _navThisFrame;
            _navThisFrame = 0;
            return v;
        }

        public bool MenuConfirm { get { bool v = _confirmThisFrame; _confirmThisFrame = false; return v; } }
        public bool MenuBack { get { bool v = _backThisFrame; _backThisFrame = false; return v; } }

        /// <summary>Gamepad button pressed this frame (consumed on read, safe to call from OnGUI).</summary>
        public bool PadPressed(KeyCode button) => _padThisFrame.Remove(button);

        public void SetCursorForGameplay(bool gameplay)
        {
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        /// <summary>Remapping: returns the first key/button pressed this frame (keyboard, mouse or pad).</summary>
        public static KeyCode PollAnyKeyDown(bool gamepad)
        {
            if (gamepad)
            {
                for (int i = 0; i < 20; i++)
                {
                    var k = (KeyCode)((int)KeyCode.JoystickButton0 + i);
                    if (Input.GetKeyDown(k)) return k;
                }
                return KeyCode.None;
            }
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k >= KeyCode.JoystickButton0) continue;
                if (Input.GetKeyDown(k)) return k;
            }
            return KeyCode.None;
        }
    }
}
