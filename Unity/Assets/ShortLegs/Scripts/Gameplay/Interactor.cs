using ShortLegs.Story;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShortLegs.Gameplay
{
    /// <summary>Left Click / South button: raycast from the camera and inspect whatever is in front.</summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private InputActionReference interact;
        [SerializeField] private InputActionReference inspectZoom;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float range = 2.5f;
        [SerializeField] private float zoomFov = 32f;
        [SerializeField] private LayerMask mask = ~0;

        private float _baseFov;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera != null) _baseFov = viewCamera.fieldOfView;
        }

        private void Update()
        {
            var no = GetComponentInParent<NetworkObject>();
            if (no != null && no.IsSpawned && !no.IsOwner) return;
            if (viewCamera == null) return;

            // RT: inspect clue closer.
            bool zoom = inspectZoom != null && inspectZoom.action.IsPressed();
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, zoom ? zoomFov : _baseFov, Time.deltaTime * 10f);

            if (interact == null || !interact.action.WasPressedThisFrame()) return;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Collide)) return;

            if (hit.collider.GetComponentInParent<ClueSpot>() is { } spot) spot.Inspect();
            else if (hit.collider.GetComponentInParent<StoryClue>() is { } storyClue) storyClue.Inspect();
            else if (hit.collider.GetComponentInParent<ScriptedSuspect>() is { } suspect) StoryCaseDirector.Instance?.Interrogate(suspect);
        }
    }
}
