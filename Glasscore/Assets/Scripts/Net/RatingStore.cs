using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Glasscore.Simulation;

namespace Glasscore.Net
{
    /// <summary>
    /// Server-side persistent rating table (tab-separated: key, rating, games). Dedicated servers keep it
    /// in their data directory so skill-based matchmaking survives restarts.
    /// </summary>
    public sealed class RatingStore
    {
        public struct Record
        {
            public int Rating;
            public int Games;
        }

        private readonly Dictionary<string, Record> _records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly string _path;

        public RatingStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "ratings.tsv");
            if (!File.Exists(_path)) return;
            foreach (string line in File.ReadAllLines(_path))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3) continue;
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rating) &&
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int games))
                    _records[parts[0]] = new Record { Rating = rating, Games = games };
            }
        }

        public Record Get(string key, int fallbackRating)
        {
            if (!string.IsNullOrEmpty(key) && _records.TryGetValue(key, out var r)) return r;
            return new Record { Rating = fallbackRating > 0 ? fallbackRating : RatingSystem.StartingRating, Games = 0 };
        }

        public void Set(string key, Record record)
        {
            if (string.IsNullOrEmpty(key)) return;
            _records[key] = record;
        }

        public void Save()
        {
            if (_path == null) return;
            var lines = new List<string>(_records.Count);
            foreach (var kv in _records)
                lines.Add(string.Join("\t", kv.Key.Replace("\t", " "), kv.Value.Rating.ToString(CultureInfo.InvariantCulture), kv.Value.Games.ToString(CultureInfo.InvariantCulture)));
            string tmp = _path + ".tmp";
            File.WriteAllLines(tmp, lines);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }
}
