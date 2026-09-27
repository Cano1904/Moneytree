using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RePlanet.Core
{
    /// <summary>
    /// Versioniertes Spielstandsformat mit Prüfsumme.
    /// Aufbau: { format, version, saved, checksum, meta, payload } – payload ist der Weltzustand als JSON-Text.
    /// </summary>
    public static class SaveCodec
    {
        public const string Format = "replanet-save";

        public static string Encode(WorldState w)
        {
            w.Version = WorldState.CurrentVersion;
            string payload = Json.Write(w.ToJson(true));
            var meta = new JObj()
                .Set("world", w.WorldName)
                .Set("planet", w.CurrentPlanet)
                .Set("planetName", GameData.Planets[w.CurrentPlanet].Name)
                .Set("playtime", Math.Round(w.PlayTime, 1))
                .Set("credits", w.Credits)
                .Set("campaign", w.CampaignDone)
                .Set("restoration", Math.Round(TotalRestoration(w) * 100, 1));
            var o = new JObj()
                .Set("format", Format)
                .Set("version", WorldState.CurrentVersion)
                .Set("saved", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Set("checksum", Hash.Fnv1a(payload).ToString("x8"))
                .Set("meta", meta)
                .Set("payload", payload);
            return Json.Write(o);
        }

        public static float TotalRestoration(WorldState w)
        {
            float sum = 0;
            foreach (var p in GameData.PlanetOrder)
                if (w.Planets.ContainsKey(p)) sum += Rules.PlanetRestoration(w, w.Planets[p]);
            return sum / GameData.PlanetOrder.Count;
        }

        public static JObj ReadMeta(string text, out string error)
        {
            error = null;
            JObj o;
            if (!Json.TryParseObj(text, out o)) { error = "Datei ist beschädigt (kein gültiges JSON)."; return null; }
            if (o.Str("format") != Format) { error = "Kein RE:PLANET-Spielstand."; return null; }
            var meta = o.Obj("meta") ?? new JObj();
            meta["saved"] = o.Str("saved", "");
            meta["version"] = o.Int("version");
            return meta;
        }

        public static WorldState Decode(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text)) { error = "Spielstand ist leer."; return null; }
            JObj o;
            if (!Json.TryParseObj(text, out o)) { error = "Spielstand ist beschädigt (kein gültiges JSON)."; return null; }
            if (o.Str("format") != Format) { error = "Kein RE:PLANET-Spielstand."; return null; }
            int version = o.Int("version", 0);
            if (version > WorldState.CurrentVersion) { error = "Spielstand stammt aus einer neueren Spielversion (" + version + ")."; return null; }
            string payload = o.Str("payload");
            if (payload == null) { error = "Spielstand enthält keine Daten."; return null; }
            string sum = o.Str("checksum", "");
            if (!string.Equals(sum, Hash.Fnv1a(payload).ToString("x8"), StringComparison.OrdinalIgnoreCase))
            { error = "Prüfsumme stimmt nicht – der Spielstand wurde beschädigt."; return null; }
            JObj p;
            if (!Json.TryParseObj(payload, out p)) { error = "Weltdaten sind beschädigt."; return null; }
            try
            {
                p = Migrate(p, version);
                var w = WorldState.FromJson(p);
                w.Version = WorldState.CurrentVersion;
                return w;
            }
            catch (Exception e)
            {
                error = "Weltdaten konnten nicht gelesen werden: " + e.Message;
                return null;
            }
        }

        /// <summary>Hebt ältere Formate schrittweise auf die aktuelle Version an.</summary>
        public static JObj Migrate(JObj p, int from)
        {
            if (from < 2)
            {
                // v1: Guthaben hieß "money", Statistiken fehlten.
                if (p.ContainsKey("money") && !p.ContainsKey("credits")) { p["credits"] = p["money"]; p.Remove("money"); }
                if (!p.ContainsKey("stats")) p["stats"] = new JObj();
            }
            if (from < 3)
            {
                // v2: Flags ohne Intro-Markierung, Planeten ohne "misc"-Teil.
                var flags = p.Obj("flags");
                if (flags != null && !flags.ContainsKey("is")) flags["is"] = true;
                var planets = p.Obj("planets");
                if (planets != null)
                    foreach (var kv in planets)
                    {
                        var ps = kv.Value as JObj;
                        if (ps != null && !ps.ContainsKey("misc")) ps["misc"] = new JObj().Set("ci", 0).Set("vis", true);
                    }
            }
            p["version"] = WorldState.CurrentVersion;
            return p;
        }
    }

    public class SlotInfo
    {
        public string Slot, File, Saved, World, Planet, Error;
        public double Playtime, Restoration;
        public long Credits;
        public bool HasBackup, Campaign;
    }

    /// <summary>Dateibasierte Spielstände mit atomarem Schreiben und Backup-Rotation.</summary>
    public class SaveStore
    {
        public readonly string Dir;
        public static readonly string[] Slots = { "auto", "slot1", "slot2", "slot3" };

        public SaveStore(string dir)
        {
            Dir = dir;
            Directory.CreateDirectory(dir);
        }

        public string PathOf(string slot) { return Path.Combine(Dir, slot + ".rpsave"); }
        public string BackupOf(string slot) { return Path.Combine(Dir, slot + ".bak.rpsave"); }

        public bool Save(string slot, WorldState w, out string error)
        {
            return SaveText(slot, SaveCodec.Encode(w), out error);
        }

        public bool SaveText(string slot, string text, out string error)
        {
            error = null;
            try
            {
                string path = PathOf(slot), tmp = path + ".tmp", bak = BackupOf(slot);
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                // Probelesen, bevor das alte Backup ersetzt wird
                string check;
                if (SaveCodec.Decode(File.ReadAllText(tmp, Encoding.UTF8), out check) == null) { error = "Schreibprüfung fehlgeschlagen: " + check; return false; }
                if (File.Exists(path))
                {
                    // Nur einen gültigen Stand als Backup behalten
                    string e2;
                    if (SaveCodec.Decode(File.ReadAllText(path, Encoding.UTF8), out e2) != null)
                    {
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(path, bak);
                    }
                    else File.Delete(path);
                }
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                error = "Speichern fehlgeschlagen: " + e.Message;
                return false;
            }
        }

        /// <summary>Lädt einen Slot; bei beschädigtem Hauptstand wird automatisch das Backup genutzt.</summary>
        public WorldState Load(string slot, out string error, out bool fromBackup)
        {
            fromBackup = false;
            error = null;
            string mainErr = null;
            string path = PathOf(slot);
            if (File.Exists(path))
            {
                try
                {
                    var w = SaveCodec.Decode(File.ReadAllText(path, Encoding.UTF8), out mainErr);
                    if (w != null) return w;
                }
                catch (Exception e) { mainErr = e.Message; }
            }
            else mainErr = "Kein Spielstand vorhanden.";
            string bak = BackupOf(slot);
            if (File.Exists(bak))
            {
                string bakErr;
                try
                {
                    var w = SaveCodec.Decode(File.ReadAllText(bak, Encoding.UTF8), out bakErr);
                    if (w != null)
                    {
                        fromBackup = true;
                        error = "Hauptspielstand unbrauchbar (" + mainErr + ") – Backup wurde geladen.";
                        return w;
                    }
                }
                catch (Exception e) { bakErr = e.Message; }
                error = mainErr + " Backup ebenfalls unbrauchbar: " + bakErr;
                return null;
            }
            error = mainErr;
            return null;
        }

        public SlotInfo Info(string slot)
        {
            var info = new SlotInfo { Slot = slot, File = PathOf(slot), HasBackup = File.Exists(BackupOf(slot)) };
            if (!File.Exists(info.File)) return info.HasBackup ? Fill(info, BackupOf(slot), true) : null;
            return Fill(info, info.File, false);
        }

        SlotInfo Fill(SlotInfo info, string file, bool isBackup)
        {
            try
            {
                string err;
                var meta = SaveCodec.ReadMeta(File.ReadAllText(file, Encoding.UTF8), out err);
                if (meta == null) { info.Error = err; return info; }
                info.Saved = meta.Str("saved", "");
                info.World = meta.Str("world", "Welt");
                info.Planet = meta.Str("planetName", "");
                info.Playtime = meta.Num("playtime");
                info.Credits = meta.Long("credits");
                info.Restoration = meta.Num("restoration");
                info.Campaign = meta.Bool("campaign");
                if (isBackup) info.Error = "Nur Backup vorhanden";
            }
            catch (Exception e) { info.Error = e.Message; }
            return info;
        }

        public List<SlotInfo> List()
        {
            var l = new List<SlotInfo>();
            foreach (var s in Slots) { var i = Info(s); if (i != null) l.Add(i); }
            return l;
        }

        public string MostRecentSlot()
        {
            string best = null; DateTime bt = DateTime.MinValue;
            foreach (var s in Slots)
            {
                var p = PathOf(s);
                if (!File.Exists(p)) p = BackupOf(s);
                if (!File.Exists(p)) continue;
                var t = File.GetLastWriteTimeUtc(p);
                if (t > bt) { bt = t; best = s; }
            }
            return best;
        }

        public bool Delete(string slot)
        {
            try
            {
                if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
                if (File.Exists(BackupOf(slot))) File.Delete(BackupOf(slot));
                return true;
            }
            catch { return false; }
        }

        public string Export(string slot, string targetDir, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(targetDir);
                string dst = Path.Combine(targetDir, "RePlanet_" + slot + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".rpsave");
                File.Copy(PathOf(slot), dst, true);
                return dst;
            }
            catch (Exception e) { error = e.Message; return null; }
        }

        public bool Import(string file, string slot, out string error)
        {
            error = null;
            try
            {
                var text = File.ReadAllText(file, Encoding.UTF8);
                if (SaveCodec.Decode(text, out error) == null) return false;
                return SaveText(slot, text, out error);
            }
            catch (Exception e) { error = e.Message; return false; }
        }
    }
}
