using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RePlanet.Core;

namespace RePlanet.DocGen
{
    /// <summary>
    /// Erzeugt die Wirtschaftstabellen (docs/WIRTSCHAFT.md) direkt aus den Spieldaten.
    /// Aufruf aus dem Repository-Wurzelordner: dotnet run --project Tools/DocGen [-- Zieldatei]
    /// Die Ausgabe ist deterministisch (keine Zeitstempel), damit Änderungen im Diff sichtbar sind.
    /// </summary>
    public static class Program
    {
        static readonly StringBuilder sb = new StringBuilder();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : Path.Combine("docs", "WIRTSCHAFT.md");
            GameData.EnsureLoaded();

            Header();
            BaseValues();
            MaterialsTable();
            TechTable();
            VehiclesTable();
            BuildingsTable();
            ProjectsTable();
            SmallCosts();
            MissionsTable();
            ContractsTable();
            DeliveriesTable();
            TrashPerPlanet();

            string text = sb.ToString().Replace("\r\n", "\n");
            string dir = Path.GetDirectoryName(Path.GetFullPath(target));
            Directory.CreateDirectory(dir);
            File.WriteAllText(target, text, new UTF8Encoding(false));
            Console.WriteLine("Geschrieben: " + Path.GetFullPath(target) + " (" + text.Length + " Zeichen)");
            return 0;
        }

        // ================================================================== Formatierung
        static string N(long v) { return v.ToString("#,0", Inv).Replace(",", "."); }
        static string D(double v, string fmt = "0.##") { return v.ToString(fmt, Inv).Replace(".", ","); }
        static string Pct(double v) { return D(v * 100, "0") + " %"; }
        static void L(string s = "") { sb.Append(s).Append('\n'); }

        static void Row(params object[] cells)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", cells.Select(c => (c ?? "").ToString().Replace("|", "\\|"))));
            sb.Append(" |\n");
        }

        /// <summary>Tabellenkopf. Ein „#“ am Anfang oder Ende eines Spaltennamens bedeutet: rechtsbündig.</summary>
        static void Head(params string[] cols)
        {
            Row(cols.Select(c => (object)c.Trim('#')).ToArray());
            sb.Append('|');
            foreach (var c in cols) sb.Append(c.StartsWith("#") || c.EndsWith("#") ? " ---: |" : " --- |");
            sb.Append('\n');
        }

        static string Mats(Dictionary<string, int> mats)
        {
            if (mats == null || mats.Count == 0) return "–";
            return string.Join(", ", mats.Select(kv => kv.Value + " " + GameData.Materials[kv.Key].Name));
        }

        static string PlanetName(string id) { return id == null ? "alle" : GameData.Planets[id].Name; }

        /// <summary>Wert einer Materialmenge bei sortiertem Verkauf (ohne Boni).</summary>
        static long SortedValue(Dictionary<string, int> mats)
        {
            long v = 0;
            foreach (var kv in mats) v += (long)GameData.SellPrice(kv.Key, Grade.Sorted, 1f) * kv.Value;
            return v;
        }

        static long DisposalValue(Dictionary<string, int> mats)
        {
            long v = 0;
            foreach (var kv in mats) v += (long)GameData.Materials[kv.Key].DisposalBonus * kv.Value;
            return v;
        }

        // ================================================================== Abschnitte
        static void Header()
        {
            L("# RE:PLANET – Wirtschaftstabellen");
            L();
            L("> Automatisch erzeugt aus `Core/Data/GameData.cs`, `Core/Sim/Rules.cs` und `Core/World/WorldGen.cs`");
            L("> mit `dotnet run --project Tools/DocGen` (aus dem Repository-Wurzelordner). **Nicht von Hand bearbeiten** –");
            L("> nach Balancing-Änderungen einfach neu erzeugen. Alle Preise in Credits, ohne Boni, sofern nicht anders angegeben.");
            L();
            L("Inhalt: [Grundwerte](#grundwerte) · [Materialien](#materialien) · [Upgrades](#upgrades-werkstatt) · [Fahrzeuge](#fahrzeuge) ·");
            L("[Gebäude](#gebäude-stützpunkt) · [Großprojekte](#großprojekte) · [Kleine Kosten](#reparaturen-ökologie-unterschlupf) ·");
            L("[Aufträge](#aufträge-missionen) · [Recyclingaufträge](#recyclingaufträge-auftragstafel) · [Lieferungen](#schrottlieferungen) ·");
            L("[Müll je Planet](#müll-je-planet-und-bereich)");
            L();
        }

        static void BaseValues()
        {
            L("## Grundwerte");
            L();
            Head("Wert", "Einstellung#");
            Row("Startguthaben", N(GameData.StartCredits) + " Credits");
            Row("Verkauf unsortiert (direkt aus dem Behälter oder Lager)", D(GameData.UnsortedFactor) + " × Materialpreis");
            Row("Verkauf sortiert", D(GameData.SortedFactor) + " × Materialpreis");
            Row("Verkauf als Ballen (" + GameData.BaleUnits + " sortierte Einheiten)", D(GameData.BaleFactor) + " × Materialpreis je Einheit");
            Row("Einkauf beim Materialhändler", D(GameData.BuyFactor) + " × Materialpreis (aufgerundet)");
            Row("Recyclingauftrag (Auftragstafel)", D(GameData.ContractFactor) + " × Materialpreis (aufgerundet)");
            Row("Lagerkapazität am Stützpunkt (Grundwert)", N(GameData.BaseStorageCap) + " Einheiten");
            Row("Grundenergie für Anlagen", D(GameData.BaseEnergy) + " E");
            Row("Bereich gilt als „Hauptmüll entfernt“ ab", Pct(GameData.AreaCleanThreshold) + " (Objekte gewichtet nach Materialeinheiten, Sperren zählen nicht)");
            Row("Koop: Gäste brauchen Host-Freigabe ab", N(GameData.GuestExpensiveThreshold) + " Credits (außer im Vertrauensmodus)");
            Row("Spieler je Sitzung", GameData.MaxPlayers.ToString(Inv));
            Row("Handelsposten auf PYRA (Projekt pyra_p1)", "+15 % auf Verkäufe, solange man sich auf PYRA befindet");
            Row("Recyclingwerk (Projekt pyra_p3)", "+20 % auf Ballen-Verkäufe auf allen Planeten");
            Row("Gefahrstoffe/Altöl", "nicht verkäuflich; Entsorgungsstation zahlt einen Bonus je Einheit");
            L();
        }

        static void MaterialsTable()
        {
            L("## Materialien");
            L();
            L("Form und Symbol unterscheiden die Materialien zusätzlich zur Farbe (Barrierefreiheit).");
            L();
            Head("Material", "Symbol", "Pressbar", "#Unsortiert/E", "#Sortiert/E", "#Ballen (10 E)", "#je E im Ballen", "#Einkauf/E", "#Entsorgung/E");
            foreach (var id in GameData.MaterialOrder)
            {
                var m = GameData.Materials[id];
                int bale = GameData.SellPrice(id, Grade.Bale, 1f);
                Row(m.Name, m.Symbol, m.Pressable ? "ja" : "nein",
                    m.Price > 0 ? N(GameData.SellPrice(id, Grade.Unsorted, 1f)) : "–",
                    m.Price > 0 ? N(GameData.SellPrice(id, Grade.Sorted, 1f)) : "–",
                    m.Price > 0 && m.Pressable ? N(bale) : "–",
                    m.Price > 0 && m.Pressable ? D(bale / (double)GameData.BaleUnits) : "–",
                    m.Buyable ? N(GameData.BuyPrice(id)) : "–",
                    m.DisposalBonus > 0 ? N(m.DisposalBonus) : "–");
            }
            L();
            L("Ballen presst die **Ballenpresse** (Gebäude) nur aus pressbaren Materialien. Die **Müllpresse** (Upgrade) halbiert dagegen");
            L("nur das Volumen im Behälter und ändert den Preis nicht.");
            L();
        }

        static void TechTable()
        {
            L("## Upgrades (Werkstatt)");
            L();
            L("Upgrades gelten für das ganze Team (Koop) und alle Planeten. Stufe 0 ist der Startzustand, „Summe“ die kumulierten Kosten.");
            L();
            Head("Upgrade", "Kategorie", "Stufe#", "#Kosten", "#Summe", "Wert", "Kaufbar");
            foreach (var id in GameData.TechOrder)
            {
                var t = GameData.Tech[id];
                long sum = 0;
                for (int i = 0; i < t.Levels.Count; i++)
                {
                    var lv = t.Levels[i];
                    sum += lv.Cost;
                    Row(i == 0 ? "**" + t.Name + "**" : "", i == 0 ? t.Category : "", i, i == 0 ? "Start" : N(lv.Cost), i == 0 ? "–" : N(sum), lv.Label, i == 0 ? Availability(t) : "");
                }
            }
            L();
            long total = 0;
            foreach (var t in GameData.Tech.Values) foreach (var lv in t.Levels) total += lv.Cost;
            L("Alle Upgrades zusammen: **" + N(total) + " Credits**.");
            L();
            L("Beschreibungen:");
            L();
            foreach (var id in GameData.TechOrder)
            {
                var t = GameData.Tech[id];
                L("- **" + t.Name + "** (" + t.Effect + "): " + t.Desc);
            }
            L();
        }

        static string Availability(TechDef t)
        {
            if (t.RequiresPlanet == null) return "sofort";
            var pd = GameData.Planets[t.RequiresPlanet];
            return pd.StartPlanet ? "sofort (gedacht für " + pd.Name + ")" : "nach Freischaltung von " + pd.Name;
        }

        static void VehiclesTable()
        {
            L("## Fahrzeuge");
            L();
            Head("Fahrzeug", "#Kosten", "#Tempo (m/s)", "#Ladung (Vol.)", "Planet", "Beschreibung");
            foreach (var v in GameData.Vehicles.Values.OrderBy(v => v.Cost))
                Row(v.Name, N(v.Cost), D(v.Speed), v.Capacity > 0 ? D(v.Capacity) : "–", PlanetName(v.Planet), v.Desc);
            L();
            Head("Raumschiff", "#Kosten", "Wirkung");
            for (int i = 0; i < GameData.ShipLevelCost.Length; i++)
                Row(GameData.ShipLevelName[i], i == 0 ? "vorhanden" : N(GameData.ShipLevelCost[i]), i == 0 ? "Reisen zwischen den drei Startplaneten" : "Freischaltung von NIVALIS (zusätzlich alle drei Großprojekte nötig)");
            L();
        }

        static void BuildingsTable()
        {
            L("## Gebäude (Stützpunkt)");
            L();
            L("Anlagen (Maschinen) arbeiten nur, wenn sie über Förderbänder mit der Stützpunktkante verbunden sind. Reicht die Energie nicht,");
            L("arbeiten alle Anlagen anteilig langsamer (Wirkungsgrad = Angebot / Bedarf).");
            L();
            Head("Gebäude", "Kategorie", "Größe", "#Credits", "Material", "#Materialwert", "Energie", "#Max.", "Voraussetzung", "Wirkung");
            foreach (var id in GameData.BuildingOrder)
            {
                var b = GameData.Buildings[id];
                string energy = b.EnergyGen > 0 ? "+" + D(b.EnergyGen) : b.EnergyUse > 0 ? "−" + D(b.EnergyUse) : "–";
                string req = b.RequiresPlanetProject != null ? "Projekt „" + GameData.Projects[b.RequiresPlanetProject].Name + "“" : "–";
                Row(b.Name, b.Category, b.W + "×" + b.H, N(b.Cost), Mats(b.Mats), N(SortedValue(b.Mats)), energy, b.MaxCount >= 99 ? "–" : b.MaxCount.ToString(Inv), req, b.Desc);
            }
            L();
        }

        static void ProjectsTable()
        {
            L("## Großprojekte");
            L();
            L("Je Bereich ein Projekt. Start nur am Projektplatz, wenn der Bereich zu mindestens " + Pct(GameData.AreaCleanThreshold) + " gereinigt ist.");
            L("Material kommt aus dem Lager des jeweiligen Planeten. „Materialwert“ = Wert des Materials bei sortiertem Verkauf.");
            L();
            foreach (var pl in GameData.PlanetOrder)
            {
                var pd = GameData.Planets[pl];
                L("### " + pd.Name + " – " + pd.Subtitle);
                L();
                Head("Bereich", "Projekt", "#Credits", "Material", "#Materialwert", "#Bauzeit (s)", "#Energie +", "Voraussetzung", "Wirkung");
                long sumC = 0, sumV = 0;
                for (int a = 0; a < 3; a++)
                {
                    var p = GameData.Projects[GameData.ProjectId(pl, a)];
                    long mv = SortedValue(p.Mats);
                    sumC += p.Credits; sumV += mv;
                    string req = p.Requires.Length == 0 ? "–" : string.Join(", ", p.Requires.Select(r => "„" + GameData.Projects[r].Name + "“"));
                    Row(pd.AreaNames[a], (p.Great ? "**" + p.Name + "** (Großprojekt)" : p.Name), N(p.Credits), Mats(p.Mats), N(mv), D(p.BuildTime, "0"), p.EnergyBonus > 0 ? D(p.EnergyBonus) : "–", req, p.Desc.Replace(" GROSSPROJEKT", ""));
                }
                Row("**Summe**", "", "**" + N(sumC) + "**", "", "**" + N(sumV) + "**", "", "", "", "");
                L();
            }
            L("NIVALIS wird freigeschaltet, wenn die Großprojekte auf TERRA, PYRA und PELAGIA abgeschlossen sind und der Sprungantrieb");
            L("(" + N(GameData.ShipLevelCost[1]) + " Credits) eingebaut ist. Die Kampagne endet mit allen vier Großprojekten.");
            L();
        }

        static void SmallCosts()
        {
            L("## Reparaturen, Ökologie, Unterschlupf");
            L();
            Head("Planet", "Reparatur (Material aus dem Lager)", "#Belohnung", "#Reparaturpunkte", "Ökologische Aktion", "#Pflanzplätze");
            foreach (var pl in GameData.PlanetOrder)
            {
                var pd = GameData.Planets[pl];
                var l = WorldGen.Get(pl);
                Row(pd.Name, pd.RepairName + ": " + Mats(Rules.RepairCost(pl)), "15", l.Repairs.Count, pd.EcoName + " (15 Credits, Bio-Modul, nach dem Projekt des Bereichs)", l.Eco.Count);
            }
            L();
            Head("Sonstiges", "Wert");
            Row("Notunterschlupf bauen", N(Rules.ShelterCost) + " Credits (höchstens " + Rules.MaxShelters + " je Planet, nicht am Stützpunkt, nicht im Wasser)");
            Row("Wachstum einer Pflanzung", D(Rules.GrowTime, "0") + " s Spielzeit");
            Row("Kran: Grundzeit zum Anheben", D(Rules.LiftTime, "0") + " s × max(1, Masse/90 kg); jeder helfende Mitspieler +75 % Tempo");
            L();
        }

        static void MissionsTable()
        {
            L("## Aufträge (Missionen)");
            L();
            Head("Planet", "Art", "Auftrag", "Ziel", "#Menge", "#Belohnung", "Kosmetik", "Voraussetzung");
            foreach (var m in GameData.Missions)
            {
                string cos = m.RewardCosmetic != null ? GameData.Cosmetics[m.RewardCosmetic].Name : "–";
                string pre = m.Prereq != null ? GameData.Missions.First(x => x.Id == m.Prereq).Title : "–";
                Row(PlanetName(m.Planet), m.Kind == "tutorial" ? "Einführung" : "Nebenauftrag", m.Title, m.Desc, m.Target, m.RewardCredits > 0 ? N(m.RewardCredits) : "–", cos, pre);
            }
            L();
            long sum = GameData.Missions.Sum(m => (long)m.RewardCredits);
            L("Summe aller Auftragsbelohnungen: **" + N(sum) + " Credits**.");
            L();
        }

        static void ContractsTable()
        {
            L("## Recyclingaufträge (Auftragstafel)");
            L();
            L("Endlos wiederholbar; verlangt sortierte Einheiten aus dem Lager. Die Aufträge laufen reihum (hier die ersten sechs je Planet).");
            L();
            Head("Planet", "Nr.#", "Material", "#Menge", "#Belohnung", "#zum Vergleich: sortiert verkauft");
            foreach (var pl in GameData.PlanetOrder)
                for (int i = 0; i < 6; i++)
                {
                    string mat; int n, reward;
                    Rules.Contract(pl, i, out mat, out n, out reward);
                    Row(i == 0 ? GameData.Planets[pl].Name : "", i + 1, GameData.Materials[mat].Name, n, N(reward), N((long)GameData.SellPrice(mat, Grade.Sorted, 1f) * n));
                }
            L();
        }

        static long TypeValue(string type) { return SortedValue(GameData.Trash[type].Yield); }

        static void DeliveriesTable()
        {
            L("## Schrottlieferungen");
            L();
            L("Am Stützpunkt bestellbar (eine Lieferung gleichzeitig), je " + GameData.DeliveryParts + " Teile aus der Liste des Planeten, reihum gewählt. " +
              "Jede Bestellung kostet eine Liefergebühr; danach startet der nächste Frachter frühestens nach " + D(GameData.DeliveryCooldown, "0") + " s Spielzeit (je Planet).");
            L();
            Head("Planet", "Mögliche Teile", "#Ø Wert je Teil (sortiert)", "#Ø Wert je Lieferung", "#Gebühr");
            foreach (var pl in GameData.PlanetOrder)
            {
                var pd = GameData.Planets[pl];
                double avg = pd.Deliveries.Average(t => (double)TypeValue(t));
                Row(pd.Name, string.Join(", ", pd.Deliveries.Select(t => GameData.Trash[t].Name)), D(avg, "0.0"), D(avg * GameData.DeliveryParts, "0"), N(GameData.DeliveryFee(pl)));
            }
            L();
        }

        static string ToolHint(TrashType t)
        {
            var parts = new List<string>();
            if (t.Crane) parts.Add("Kran");
            if (t.Oil) parts.Add("Filtermodul");
            if (!t.Crane && !t.Oil)
            {
                if (t.Grab)
                {
                    var grab = GameData.Tech["grab"];
                    int lvl = -1;
                    for (int i = 0; i < grab.Levels.Count; i++) if (grab.Levels[i].Value >= t.Mass) { lvl = i; break; }
                    if (lvl >= 0) parts.Add(lvl == 0 ? "Greifarm" : "Greifarm St. " + lvl);
                }
                if (t.Vacuum) parts.Add("Sauger");
                if (t.Magnet) parts.Add(t.MinMagnet > 1 ? "Magnet St. " + t.MinMagnet : "Magnet");
            }
            if (t.CutInto != null) parts.Add("Schneidgerät");
            if (t.Hazard > 0) parts.Add("Gefahrgut Kl. " + t.Hazard);
            return string.Join(", ", parts);
        }

        class Agg { public int Count, Gate, Frozen, Under; public long Units, Value, Disposal; }

        static void TrashPerPlanet()
        {
            L("## Müll je Planet und Bereich");
            L();
            L("Ausgangszustand der deterministisch erzeugten Welten (`WorldGen.Get(planet)`). „Einheiten“ = Materialeinheiten nach dem");
            L("Einlagern, „Wert“ = Verkaufswert dieser Einheiten sortiert (ohne Boni), „Entsorgung“ = Bonus der Entsorgungsstation für");
            L("Gefahrstoffe/Altöl. Sperren (Zugänge zum nächsten Bereich) sind enthalten und in der Spalte „Sperre“ gezählt.");
            L("Zerlegen (Schneidgerät) ändert die Summe nicht wesentlich; Lieferungen und Zerlegeteile kommen im Spiel hinzu.");
            L();
            var overview = new List<object[]>();
            foreach (var pl in GameData.PlanetOrder)
            {
                var pd = GameData.Planets[pl];
                var l = WorldGen.Get(pl);
                L("### " + pd.Name + " – " + pd.Subtitle);
                L();
                var areaSum = new Agg[3];
                var matUnits = new Dictionary<string, long>();
                for (int a = 0; a < 3; a++)
                {
                    areaSum[a] = new Agg();
                    var per = new Dictionary<string, Agg>();
                    foreach (var t in l.Trash)
                    {
                        if (t.Area != a) continue;
                        Agg g;
                        if (!per.TryGetValue(t.Type, out g)) { g = new Agg(); per[t.Type] = g; }
                        var def = t.Def;
                        g.Count++;
                        if (t.Gate >= 0) g.Gate++;
                        if (t.Frozen) g.Frozen++;
                        if (t.Underwater) g.Under++;
                        g.Units += def.TotalUnits;
                        g.Value += TypeValue(t.Type);
                        g.Disposal += DisposalValue(def.Yield);
                        foreach (var kv in def.Yield) { long cur; matUnits.TryGetValue(kv.Key, out cur); matUnits[kv.Key] = cur + kv.Value; }
                    }
                    L("**Bereich " + (a + 1) + ": " + pd.AreaNames[a] + "** – " + pd.AreaDesc[a]);
                    L();
                    bool showFrozen = per.Values.Any(g => g.Frozen > 0), showUnder = per.Values.Any(g => g.Under > 0);
                    var cols = new List<string> { "Müllart", "#Anzahl", "#Sperre", "#E je Objekt", "#Einheiten", "#Wert", "#Entsorgung", "Werkzeug" };
                    if (showFrozen) cols.Insert(3, "#gefroren");
                    if (showUnder) cols.Insert(3, "#unter Wasser");
                    Head(cols.ToArray());
                    foreach (var kv in per.OrderByDescending(k => k.Value.Value).ThenByDescending(k => k.Value.Units).ThenBy(k => k.Key, StringComparer.Ordinal))
                    {
                        var t = GameData.Trash[kv.Key];
                        var g = kv.Value;
                        var cells = new List<object> { t.Name, g.Count, g.Gate > 0 ? g.Gate.ToString(Inv) : "–", t.TotalUnits, N(g.Units), N(g.Value), g.Disposal > 0 ? N(g.Disposal) : "–", ToolHint(t) };
                        if (showFrozen) cells.Insert(3, g.Frozen > 0 ? g.Frozen.ToString(Inv) : "–");
                        if (showUnder) cells.Insert(3, g.Under > 0 ? g.Under.ToString(Inv) : "–");
                        Row(cells.ToArray());
                        areaSum[a].Count += g.Count; areaSum[a].Gate += g.Gate; areaSum[a].Units += g.Units; areaSum[a].Value += g.Value; areaSum[a].Disposal += g.Disposal;
                        areaSum[a].Frozen += g.Frozen; areaSum[a].Under += g.Under;
                    }
                    var sumCells = new List<object> { "**Summe**", "**" + areaSum[a].Count + "**", areaSum[a].Gate > 0 ? areaSum[a].Gate.ToString(Inv) : "–", "", "**" + N(areaSum[a].Units) + "**", "**" + N(areaSum[a].Value) + "**", areaSum[a].Disposal > 0 ? "**" + N(areaSum[a].Disposal) + "**" : "–", "" };
                    if (showFrozen) sumCells.Insert(3, areaSum[a].Frozen.ToString(Inv));
                    if (showUnder) sumCells.Insert(3, areaSum[a].Under.ToString(Inv));
                    Row(sumCells.ToArray());
                    L();
                }
                // Planetenübersicht
                L("**" + pd.Name + " gesamt**");
                L();
                Head("Bereich", "#Objekte", "#Einheiten", "#Wert (sortiert)", "#Entsorgung", "#Projektkosten (Credits)");
                long tc = 0, tu = 0, tv = 0, td = 0, tp = 0;
                for (int a = 0; a < 3; a++)
                {
                    var p = GameData.Projects[GameData.ProjectId(pl, a)];
                    Row(pd.AreaNames[a], areaSum[a].Count, N(areaSum[a].Units), N(areaSum[a].Value), areaSum[a].Disposal > 0 ? N(areaSum[a].Disposal) : "–", N(p.Credits));
                    tc += areaSum[a].Count; tu += areaSum[a].Units; tv += areaSum[a].Value; td += areaSum[a].Disposal; tp += p.Credits;
                }
                Row("**Summe**", "**" + N(tc) + "**", "**" + N(tu) + "**", "**" + N(tv) + "**", td > 0 ? "**" + N(td) + "**" : "–", "**" + N(tp) + "**");
                L();
                overview.Add(new object[] { pd.Name, N(tc), N(tu), N(tv), td > 0 ? N(td) : "–" });

                // Materialbilanz
                L("**Materialbilanz " + pd.Name + "** (Ausgangsmüll vs. Bedarf der drei Projekte und aller Reparaturen auf diesem Planeten)");
                L();
                var need = new Dictionary<string, long>();
                for (int a = 0; a < 3; a++)
                    foreach (var kv in GameData.Projects[GameData.ProjectId(pl, a)].Mats) { long c; need.TryGetValue(kv.Key, out c); need[kv.Key] = c + kv.Value; }
                var rep = new Dictionary<string, long>();
                foreach (var kv in Rules.RepairCost(pl)) rep[kv.Key] = (long)kv.Value * l.Repairs.Count;
                Head("Material", "#im Müll", "#Projekte", "#Reparaturen", "#Rest");
                foreach (var id in GameData.MaterialOrder)
                {
                    long have, pn, rn;
                    matUnits.TryGetValue(id, out have); need.TryGetValue(id, out pn); rep.TryGetValue(id, out rn);
                    if (have == 0 && pn == 0 && rn == 0) continue;
                    long rest = have - pn - rn;
                    Row(GameData.Materials[id].Name, N(have), pn > 0 ? N(pn) : "–", rn > 0 ? N(rn) : "–", rest < 0 ? "**" + N(rest) + "** (zukaufen/liefern)" : N(rest));
                }
                L();
            }
            L("### Alle Planeten");
            L();
            Head("Planet", "#Objekte", "#Einheiten", "#Wert (sortiert)", "#Entsorgung");
            foreach (var r in overview) Row(r);
            L();
        }
    }
}
