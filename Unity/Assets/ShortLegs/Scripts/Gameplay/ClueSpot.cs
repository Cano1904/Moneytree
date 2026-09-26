using ShortLegs.Networking;
using Unity.Netcode;
using UnityEngine;

namespace ShortLegs.Gameplay
{
    /// <summary>
    /// A searchable place in the map. Clients only know whether a spot is searchable and whether it was
    /// searched — what the clue proves stays on the server.
    /// </summary>
    public sealed class ClueSpot : NetworkBehaviour
    {
        [Tooltip("Name shown if the Liar plants a false clue here.")]
        [SerializeField] private string decoyName = "Torn Receipt";
        [SerializeField, TextArea] private string decoyDescription = "Crumpled, still warm. Someone dropped it in a hurry.";
        [SerializeField] private GameObject searchableVisual;
        [SerializeField] private GameObject discoveredVisual;

        public readonly NetworkVariable<bool> Searchable = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<bool> Discovered = new NetworkVariable<bool>(false);

        public int Index { get; private set; } = -1;
        public string DecoyName => decoyName;
        public string DecoyDescription => decoyDescription;

        public override void OnNetworkSpawn()
        {
            Searchable.OnValueChanged += (_, __) => Refresh();
            Discovered.OnValueChanged += (_, __) => Refresh();
            Refresh();
        }

        public void ServerSetup(int index, bool hasClue)
        {
            Index = index;
            SetIndexRpc(index);
            Searchable.Value = hasClue;
            Discovered.Value = false;
        }

        public void ServerMarkDiscovered() => Discovered.Value = true;

        [Rpc(SendTo.ClientsAndHost)]
        private void SetIndexRpc(int index) => Index = index;

        /// <summary>Called by the interaction raycast (Left Click / South button).</summary>
        public void Inspect()
        {
            if (Index < 0 || Discovered.Value || ShortLegsPlayer.Local == null) return;
            ShortLegsPlayer.Local.InspectSpotRpc(Index);
        }

        private void Refresh()
        {
            if (searchableVisual != null) searchableVisual.SetActive(Searchable.Value && !Discovered.Value);
            if (discoveredVisual != null) discoveredVisual.SetActive(Discovered.Value);
        }
    }
}
