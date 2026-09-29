using System;
using Glasscore.Simulation;
using Raylib_cs;

namespace Glasscore.Desktop
{
    /// <summary>Keyboard/mouse (remappable) + gamepad → PlayerInput per 60 Hz tick, with tap latching.</summary>
    public sealed class DesktopInput
    {
        private const float Dead = 0.18f;
        private readonly DesktopConfig _cfg;
        private readonly LookState _look;
        private InputButtons _latched;
        private int _nav, _lastNav;
        private float _navRepeat;
        private bool _confirm, _back, _pause;

        public bool Gameplay;
        public bool UsingGamepad { get; private set; }

        public DesktopInput(DesktopConfig cfg, LookState look) { _cfg = cfg; _look = look; }

        private static bool Pad => Raylib.IsGamepadAvailable(0);
        private static float Axis(GamepadAxis a) => Pad ? Raylib.GetGamepadAxisMovement(0, a) : 0f;
        private static float DeadZone(float v) => Math.Abs(v) < Dead ? 0f : Math.Sign(v) * (Math.Abs(v) - Dead) / (1f - Dead);
        private static bool PadDown(GamepadButton b) => Pad && Raylib.IsGamepadButtonDown(0, b);
        private static bool PadHit(GamepadButton b) => Pad && Raylib.IsGamepadButtonPressed(0, b);

        private bool Down(Bind b)
        {
            int code = _cfg.Bindings[(int)b];
            if (code == -1) return Raylib.IsMouseButtonDown(MouseButton.Left);
            if (code == -2) return Raylib.IsMouseButtonDown(MouseButton.Right);
            if (code == -3) return Raylib.IsMouseButtonDown(MouseButton.Middle);
            return code > 0 && Raylib.IsKeyDown((KeyboardKey)code);
        }

        private bool Hit(Bind b)
        {
            int code = _cfg.Bindings[(int)b];
            if (code == -1) return Raylib.IsMouseButtonPressed(MouseButton.Left);
            if (code == -2) return Raylib.IsMouseButtonPressed(MouseButton.Right);
            if (code == -3) return Raylib.IsMouseButtonPressed(MouseButton.Middle);
            return code > 0 && Raylib.IsKeyPressed((KeyboardKey)code);
        }

        private bool FireHeld => Down(Bind.Fire) || Axis(GamepadAxis.RightTrigger) > 0.3f || PadDown(GamepadButton.RightTrigger2);
        private bool AnchorHeld => Down(Bind.Anchor) || Axis(GamepadAxis.LeftTrigger) > 0.3f || PadDown(GamepadButton.LeftTrigger2);

        public void Update(float dt)
        {
            // Menu navigation (once per frame).
            int dir = 0;
            float stick = Axis(GamepadAxis.LeftY);
            if (Raylib.IsKeyDown(KeyboardKey.Up) || stick < -0.5f || PadDown(GamepadButton.LeftFaceUp)) dir = -1;
            else if (Raylib.IsKeyDown(KeyboardKey.Down) || stick > 0.5f || PadDown(GamepadButton.LeftFaceDown)) dir = 1;
            _nav = 0;
            if (dir == 0) { _lastNav = 0; _navRepeat = 0; }
            else if (dir != _lastNav) { _lastNav = dir; _navRepeat = 0.35f; _nav = dir; }
            else if ((_navRepeat -= dt) <= 0f) { _navRepeat = 0.12f; _nav = dir; }
            if (_nav != 0) { UsingGamepad = Pad; Sfx.Play(Sfx.TinkHigh, 0.35f); }
            _confirm = PadHit(GamepadButton.RightFaceDown);
            _back = Raylib.IsKeyPressed(KeyboardKey.Escape) || PadHit(GamepadButton.RightFaceRight);
            _pause = Hit(Bind.Pause) || PadHit(GamepadButton.MiddleRight);

            if (!Gameplay) { _latched = InputButtons.None; return; }

            System.Numerics.Vector2 md = Raylib.GetMouseDelta();
            float rx = DeadZone(Axis(GamepadAxis.RightX)), ry = DeadZone(Axis(GamepadAxis.RightY));
            if (Math.Abs(rx) + Math.Abs(ry) > 0.01f) UsingGamepad = true;
            else if (Math.Abs(md.X) + Math.Abs(md.Y) > 0.5f) UsingGamepad = false;
            float inv = _cfg.InvertY ? -1f : 1f;
            // raylib is right-handed: +X appears on the left, so turning right means decreasing yaw.
            _look.Yaw -= md.X * 0.1f * _cfg.MouseSensitivity + rx * _cfg.GamepadLookSpeed * dt;
            _look.Yaw = (_look.Yaw % 360f + 540f) % 360f - 180f;
            _look.Pitch += (md.Y * 0.1f * _cfg.MouseSensitivity + ry * _cfg.GamepadLookSpeed * dt) * inv;
            _look.Pitch = Math.Clamp(_look.Pitch, -89f, 89f);

            if (Hit(Bind.Jump) || PadHit(GamepadButton.RightFaceDown)) _latched |= InputButtons.Jump;
            if (Hit(Bind.NextWeapon) || PadHit(GamepadButton.RightTrigger1)) _latched |= InputButtons.NextWeapon;
            if (Hit(Bind.PrevWeapon) || PadHit(GamepadButton.LeftTrigger1)) _latched |= InputButtons.PrevWeapon;
            if (Hit(Bind.Anchor)) _latched |= InputButtons.Anchor;
            if (Hit(Bind.Fire)) _latched |= InputButtons.Fire;
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel > 0.01f) _latched |= InputButtons.NextWeapon;
            else if (wheel < -0.01f) _latched |= InputButtons.PrevWeapon;
        }

        public PlayerInput Sample(uint seq)
        {
            var input = new PlayerInput { Sequence = seq, Yaw = _look.Yaw, Pitch = _look.Pitch };
            if (!Gameplay) return input; // pause: neutral input, the match keeps running
            float x = 0, y = 0;
            if (Down(Bind.Right)) x -= 1f;   // raylib/world: +X is to the left when facing +Z
            if (Down(Bind.Left)) x += 1f;
            if (Down(Bind.Forward)) y += 1f;
            if (Down(Bind.Back)) y -= 1f;
            x -= DeadZone(Axis(GamepadAxis.LeftX));
            y -= DeadZone(Axis(GamepadAxis.LeftY));
            input.MoveX = Math.Clamp(x, -1f, 1f);
            input.MoveY = Math.Clamp(y, -1f, 1f);
            InputButtons held = InputButtons.None;
            if (Down(Bind.Jump) || PadDown(GamepadButton.RightFaceDown)) held |= InputButtons.Jump;
            if (FireHeld) held |= InputButtons.Fire;
            if (AnchorHeld) held |= InputButtons.Anchor;
            if (Down(Bind.NextWeapon) || PadDown(GamepadButton.RightTrigger1)) held |= InputButtons.NextWeapon;
            if (Down(Bind.PrevWeapon) || PadDown(GamepadButton.LeftTrigger1)) held |= InputButtons.PrevWeapon;
            input.Buttons = held | _latched;
            _latched = InputButtons.None;
            return input;
        }

        public int MenuNavigate() { int v = _nav; _nav = 0; return v; }
        public bool MenuConfirm { get { bool v = _confirm; _confirm = false; return v; } }
        public bool MenuBack { get { bool v = _back; _back = false; return v; } }
        public bool PausePressed { get { bool v = _pause; _pause = false; return v; } }
        public bool PadPressed(GamepadButton b) => PadHit(b);

        /// <summary>Remapping capture: first key or mouse button pressed (0 = none).</summary>
        public static int PollAnyBinding()
        {
            int key = Raylib.GetKeyPressed();
            if (key > 0) return key;
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) return -1;
            if (Raylib.IsMouseButtonPressed(MouseButton.Right)) return -2;
            if (Raylib.IsMouseButtonPressed(MouseButton.Middle)) return -3;
            return 0;
        }
    }
}
