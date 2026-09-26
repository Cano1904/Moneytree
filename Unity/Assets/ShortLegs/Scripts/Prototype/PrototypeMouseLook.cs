using UnityEngine;
using UnityEngine.InputSystem;

namespace ShortLegs.Prototype
{
    /// <summary>Minimal-Kamera für die Testszene: Maus / rechter Stick drehen den Spieler (Yaw) und neigen die Kamera (Pitch).</summary>
    public sealed class PrototypeMouseLook : MonoBehaviour
    {
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float stickSensitivity = 120f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-30f, 60f);

        private float _pitch;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            if (pitchPivot != null) _pitch = pitchPivot.localEulerAngles.x;
        }

        private void Update()
        {
            Vector2 delta = Vector2.zero;
            if (Mouse.current != null) delta += Mouse.current.delta.ReadValue() * mouseSensitivity;
            if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * stickSensitivity * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f);
            if (pitchPivot != null)
            {
                _pitch = Mathf.Clamp(_pitch - delta.y, pitchLimits.x, pitchLimits.y);
                pitchPivot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}
