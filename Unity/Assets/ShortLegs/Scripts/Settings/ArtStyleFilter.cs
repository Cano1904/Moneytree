using UnityEngine;
using UnityEngine.Rendering;

namespace ShortLegs.Settings
{
    /// <summary>
    /// Swaps post-processing between Classic Noir (desaturated, red accents kept by the NoirRedKeep
    /// shader pass) and Vibrant Comic-Book (halftone + ink outline). Also adds the reduced-flashing mode.
    /// </summary>
    public sealed class ArtStyleFilter : MonoBehaviour
    {
        [SerializeField] private Volume noirVolume;
        [SerializeField] private Volume comicVolume;
        [Tooltip("Global keyword read by the lie-detector flash shader.")]
        [SerializeField] private string reduceFlashKeyword = "SHORTLEGS_REDUCE_FLASH";

        private void OnEnable()
        {
            if (SettingsManager.Instance == null) return;
            SettingsManager.Instance.Applied += Apply;
            Apply(SettingsManager.Instance.Current);
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Applied -= Apply;
        }

        private void Apply(GameSettings s)
        {
            bool noir = s.ArtStyle == ArtStyle.ClassicNoir;
            if (noirVolume != null) noirVolume.weight = noir ? 1f : 0f;
            if (comicVolume != null) comicVolume.weight = noir ? 0f : 1f;
            if (s.ReduceFlashing) Shader.EnableKeyword(reduceFlashKeyword);
            else Shader.DisableKeyword(reduceFlashKeyword);
        }
    }
}
