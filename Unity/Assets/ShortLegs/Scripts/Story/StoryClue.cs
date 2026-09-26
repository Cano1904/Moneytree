using UnityEngine;

namespace ShortLegs.Story
{
    /// <summary>A clue in a Story Mode scene. The id must match a clue in the CaseDefinition.</summary>
    public sealed class StoryClue : MonoBehaviour
    {
        [SerializeField] private string clueId;
        [SerializeField] private GameObject highlight;

        public string ClueId => clueId;

        public void Inspect()
        {
            if (StoryCaseDirector.Instance == null || !StoryCaseDirector.Instance.Discover(clueId)) return;
            if (highlight != null) highlight.SetActive(false);
        }
    }
}
