using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShortLegs.Story
{
    /// <summary>Evidence Archive unlocks (cases, lore, hats) saved next to the settings profile.</summary>
    public static class Progression
    {
        [System.Serializable]
        private sealed class Save { public List<string> Unlocked = new List<string>(); }

        private static Save _save;
        private static string PathOnDisk => Path.Combine(Application.persistentDataPath, "archive.json");

        private static Save Data
        {
            get
            {
                if (_save != null) return _save;
                try { _save = File.Exists(PathOnDisk) ? JsonUtility.FromJson<Save>(File.ReadAllText(PathOnDisk)) : null; }
                catch (IOException e) { Debug.LogWarning($"[ShortLegs] archive load failed: {e.Message}"); }
                return _save ??= new Save();
            }
        }

        public static bool IsUnlocked(string id) => Data.Unlocked.Contains(id);
        public static IReadOnlyList<string> All => Data.Unlocked;

        public static void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.Unlocked.Contains(id)) return;
            Data.Unlocked.Add(id);
            File.WriteAllText(PathOnDisk, JsonUtility.ToJson(Data, true));
        }
    }
}
