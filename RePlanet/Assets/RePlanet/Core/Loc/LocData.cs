using System;
using System.Collections.Generic;

namespace RePlanet.Core
{
    public static partial class Loc
    {
        struct DataField { public string De; public Action<string> Set; }
        static List<DataField> dataFields;
        static string dataLang = "de";

        /// <summary>
        /// Übersetzt die Anzeigetexte der Datentabellen (<see cref="GameData"/>: Materialien, Müll, Upgrades, Fahrzeuge,
        /// Gebäude, Planeten, Projekte, Aufträge, Fundstücke, Kosmetik; Stufennamen, Werkzeugnamen) an Ort und Stelle in die
        /// aktuelle Sprache. Die deutschen Originale werden beim ersten Aufruf gemerkt, ein Wechsel zurück stellt sie wieder her.
        /// Nur im Spielprozess aufrufen (Einstellungen); der dedizierte Server bleibt deutsch. IDs bleiben unverändert.
        /// Namen aus der Weltgenerierung (Lichtpunkte, Aussichtspunkte) werden bei der Anzeige übersetzt.
        /// </summary>
        public static void ApplyToData()
        {
            lock (buildLock)
            {
                if (dataFields == null) dataFields = CollectDataFields();
                if (dataLang == lang) return;
                dataLang = lang;
            }
            foreach (var f in dataFields) f.Set(T(f.De));
        }

        /// <summary>Alle deutschen Datentexte (für Tests: jeder braucht eine Übersetzung).</summary>
        public static List<string> DataTexts()
        {
            lock (buildLock) if (dataFields == null) dataFields = CollectDataFields();
            var l = new List<string>();
            foreach (var f in dataFields) if (!string.IsNullOrEmpty(f.De)) l.Add(f.De);
            return l;
        }

        static List<DataField> CollectDataFields()
        {
            GameData.EnsureLoaded();
            var l = new List<DataField>();
            Action<string, Action<string>> add = (de, set) => { if (!string.IsNullOrEmpty(de)) l.Add(new DataField { De = de, Set = set }); };
            foreach (var m in GameData.Materials.Values) { var x = m; add(x.Name, v => x.Name = v); }
            foreach (var t in GameData.Trash.Values) { var x = t; add(x.Name, v => x.Name = v); add(x.Desc, v => x.Desc = v); }
            foreach (var t in GameData.Tech.Values)
            {
                var x = t;
                add(x.Name, v => x.Name = v); add(x.Desc, v => x.Desc = v); add(x.Category, v => x.Category = v); add(x.Effect, v => x.Effect = v);
                foreach (var lv in x.Levels) { var y = lv; add(y.Label, v => y.Label = v); }
            }
            foreach (var t in GameData.Vehicles.Values) { var x = t; add(x.Name, v => x.Name = v); add(x.Desc, v => x.Desc = v); }
            foreach (var t in GameData.Buildings.Values) { var x = t; add(x.Name, v => x.Name = v); add(x.Desc, v => x.Desc = v); add(x.Category, v => x.Category = v); }
            foreach (var p in GameData.Planets.Values)
            {
                var x = p;
                add(x.Subtitle, v => x.Subtitle = v); add(x.Description, v => x.Description = v); add(x.UnlockHint, v => x.UnlockHint = v);
                add(x.EcoName, v => x.EcoName = v); add(x.RepairName, v => x.RepairName = v); add(x.Mood, v => x.Mood = v);
                add(x.StormName, v => x.StormName = v); add(x.ShelterName, v => x.ShelterName = v);
                for (int i = 0; i < x.AreaNames.Length; i++) { int k = i; add(x.AreaNames[k], v => x.AreaNames[k] = v); }
                for (int i = 0; i < x.AreaDesc.Length; i++) { int k = i; add(x.AreaDesc[k], v => x.AreaDesc[k] = v); }
                if (x.Gates != null) foreach (var g in x.Gates) { var y = g; if (y != null) add(y.Hint, v => y.Hint = v); }
            }
            foreach (var p in GameData.Projects.Values) { var x = p; add(x.Name, v => x.Name = v); add(x.Desc, v => x.Desc = v); }
            foreach (var m in GameData.Missions) { var x = m; add(x.Title, v => x.Title = v); add(x.Desc, v => x.Desc = v); }
            foreach (var d in GameData.Lore.Values) { var x = d; add(x.Title, v => x.Title = v); add(x.Text, v => x.Text = v); }
            foreach (var c in GameData.Cosmetics.Values) { var x = c; add(x.Name, v => x.Name = v); add(x.Hint, v => x.Hint = v); }
            for (int i = 0; i < GameData.ShipLevelName.Length; i++) { int k = i; add(GameData.ShipLevelName[k], v => GameData.ShipLevelName[k] = v); }
            for (int i = 0; i < Rules.StageNames.Length; i++) { int k = i; add(Rules.StageNames[k], v => Rules.StageNames[k] = v); }
            return l;
        }
    }
}
