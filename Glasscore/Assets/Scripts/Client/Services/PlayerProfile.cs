using System;
using System.Collections;
using System.IO;
using System.Text;
using Glasscore.Simulation;
using UnityEngine;
using UnityEngine.Networking;

namespace Glasscore.Client
{
    /// <summary>Cosmetic loadout + identity. Serialized with JsonUtility.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string PlayerName = "Glazier";
        public string PlayerKey = string.Empty;
        public int Rating = RatingSystem.StartingRating;
        public int[] WeaponSkins = new int[4];
        public int Trail;
        public long UpdatedUnixMs;

        public byte PrimarySkin => (byte)(WeaponSkins != null && WeaponSkins.Length > 0 ? WeaponSkins[0] : 0);

        public void EnsureValid()
        {
            if (string.IsNullOrEmpty(PlayerKey)) PlayerKey = Guid.NewGuid().ToString("N");
            if (WeaponSkins == null || WeaponSkins.Length != WeaponCatalog.Count) WeaponSkins = new int[WeaponCatalog.Count];
            PlayerName = LobbyRules.SanitizePlayerName(PlayerName);
            for (int i = 0; i < WeaponSkins.Length; i++) WeaponSkins[i] = Mathf.Clamp(WeaponSkins[i], 0, Cosmetics.WeaponSkins.Length - 1);
            Trail = Mathf.Clamp(Trail, 0, Cosmetics.Trails.Length - 1);
        }
    }

    public static class Cosmetics
    {
        public struct Skin
        {
            public string Name;
            public Color Primary;
            public Color Emission;
        }

        public static readonly Skin[] WeaponSkins =
        {
            new Skin { Name = "Rohstein", Primary = new Color(0.45f, 0.45f, 0.48f), Emission = new Color(0f, 1f, 1f) },
            new Skin { Name = "Neon Cyan", Primary = new Color(0.05f, 0.25f, 0.3f), Emission = new Color(0f, 1f, 1f) * 2f },
            new Skin { Name = "Magma", Primary = new Color(0.25f, 0.05f, 0.02f), Emission = new Color(1f, 0.35f, 0f) * 2f },
            new Skin { Name = "Amethyst", Primary = new Color(0.2f, 0.05f, 0.3f), Emission = new Color(0.7f, 0.2f, 1f) * 2f },
            new Skin { Name = "Gold Leaf", Primary = new Color(0.55f, 0.42f, 0.1f), Emission = new Color(1f, 0.85f, 0.3f) },
            new Skin { Name = "Frosted", Primary = new Color(0.8f, 0.9f, 1f), Emission = new Color(0.6f, 0.9f, 1f) },
        };

        public struct Trail
        {
            public string Name;
            public Color Start;
            public Color End;
        }

        public static readonly Trail[] Trails =
        {
            new Trail { Name = "None", Start = new Color(0, 0, 0, 0), End = new Color(0, 0, 0, 0) },
            new Trail { Name = "Cyan Pulse", Start = new Color(0f, 1f, 1f, 0.9f), End = new Color(0f, 0.4f, 1f, 0f) },
            new Trail { Name = "Ember", Start = new Color(1f, 0.5f, 0.1f, 0.9f), End = new Color(1f, 0f, 0f, 0f) },
            new Trail { Name = "Prism", Start = new Color(1f, 0.2f, 0.8f, 0.9f), End = new Color(0.2f, 1f, 0.6f, 0f) },
            new Trail { Name = "Glass Dust", Start = new Color(1f, 1f, 1f, 0.8f), End = new Color(0.7f, 0.9f, 1f, 0f) },
        };

        public static Color SlotColor(int slot)
        {
            switch (slot % 8)
            {
                case 0: return new Color(0f, 1f, 1f);
                case 1: return new Color(1f, 0.25f, 0.55f);
                case 2: return new Color(1f, 0.8f, 0.1f);
                case 3: return new Color(0.4f, 1f, 0.3f);
                case 4: return new Color(0.65f, 0.4f, 1f);
                case 5: return new Color(1f, 0.5f, 0.1f);
                case 6: return new Color(0.3f, 0.6f, 1f);
                default: return new Color(0.95f, 0.95f, 0.95f);
            }
        }
    }

    /// <summary>
    /// "Saved to cloud database": the profile is written locally first (always works offline), then
    /// pushed to a cloud endpoint if one is configured (GLASSCORE_CLOUD_URL env var or cloud_url.txt
    /// next to the executable). The endpoint receives PUT {url}/profiles/{playerKey} with the JSON body
    /// and answers GET with the same document; the newer UpdatedUnixMs wins on conflict.
    /// </summary>
    public sealed class CloudSave
    {
        private readonly string _localPath;
        private readonly string _cloudUrl;

        public string Status { get; private set; } = "Local";
        public bool CloudEnabled => !string.IsNullOrEmpty(_cloudUrl);

        public CloudSave()
        {
            _localPath = Path.Combine(Application.persistentDataPath, "profile.json");
            _cloudUrl = Environment.GetEnvironmentVariable("GLASSCORE_CLOUD_URL");
            string file = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "cloud_url.txt");
            if (string.IsNullOrEmpty(_cloudUrl) && File.Exists(file)) _cloudUrl = File.ReadAllText(file).Trim();
            if (!string.IsNullOrEmpty(_cloudUrl)) _cloudUrl = _cloudUrl.TrimEnd('/');
        }

        public PlayerProfile LoadLocal()
        {
            PlayerProfile p = null;
            try
            {
                if (File.Exists(_localPath)) p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(_localPath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CloudSave] local profile unreadable: " + ex.Message);
            }
            if (p == null)
            {
                p = new PlayerProfile { PlayerName = "Glazier" + UnityEngine.Random.Range(100, 999) };
            }
            p.EnsureValid();
            return p;
        }

        public void SaveLocal(PlayerProfile p)
        {
            p.EnsureValid();
            p.UpdatedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            try
            {
                File.WriteAllText(_localPath, JsonUtility.ToJson(p, true));
                Status = CloudEnabled ? "Syncing…" : "Saved locally";
            }
            catch (Exception ex)
            {
                Status = "Save failed: " + ex.Message;
            }
        }

        public IEnumerator Push(PlayerProfile p)
        {
            if (!CloudEnabled) yield break;
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));
            using (var req = new UnityWebRequest($"{_cloudUrl}/profiles/{p.PlayerKey}", "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                Status = req.result == UnityWebRequest.Result.Success ? "Saved to cloud" : "Cloud offline (saved locally)";
            }
        }

        /// <summary>Pulls the cloud copy and adopts it if it is newer than the local one.</summary>
        public IEnumerator Pull(PlayerProfile local, Action<PlayerProfile> adopt)
        {
            if (!CloudEnabled) yield break;
            using (var req = UnityWebRequest.Get($"{_cloudUrl}/profiles/{local.PlayerKey}"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Status = "Cloud offline"; yield break; }
                PlayerProfile remote = null;
                try { remote = JsonUtility.FromJson<PlayerProfile>(req.downloadHandler.text); }
                catch (Exception) { }
                if (remote != null && remote.UpdatedUnixMs > local.UpdatedUnixMs)
                {
                    remote.EnsureValid();
                    adopt(remote);
                    Status = "Loaded from cloud";
                }
                else Status = "Cloud in sync";
            }
        }
    }
}
