using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RePlanet.Core;

/// <summary>
/// Englische Übersetzung: vollständig (jeder deutsche Anzeigetext hat einen Eintrag) und formal richtig
/// (gleiche Platzhalter). Fehlende Einträge listet „dotnet run -c Release -- locmissing“.
/// </summary>
public static class LocTests
{
    static readonly Regex Wrapped = new Regex(@"\b(?:L|Loc\.T|Loc\.F)\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
    static readonly Regex Literal = new Regex(@"(?<![\w@$\\])""((?:[^""\\\n]|\\.)*)""", RegexOptions.Compiled);
    static readonly Regex HasWord = new Regex(@"[A-Za-zÄÖÜäöüß]{2,}", RegexOptions.Compiled);

    static string Src { get { return Path.Combine(TestKit.RepoRoot(), "RePlanet", "Assets", "RePlanet"); } }

    static string Unescape(string s) { return Regex.Unescape(s); }

    /// <summary>Texte, die exakt (oder über eine Vorlage) übersetzbar sein müssen: (Text, Herkunft).</summary>
    public static List<KeyValuePair<string, string>> RequiredTexts()
    {
        var req = new List<KeyValuePair<string, string>>();
        Action<string, string> add = (t, from) => { if (!string.IsNullOrEmpty(t) && HasWord.IsMatch(t) && Regex.IsMatch(t, "[a-zäöüß]")) req.Add(new KeyValuePair<string, string>(t, from)); };
        // 1) In der Laufzeitschicht als Anzeigetext markierte Literale
        foreach (var f in Directory.GetFiles(Path.Combine(Src, "Runtime"), "*.cs", SearchOption.AllDirectories))
        {
            int no = 0;
            foreach (var line in File.ReadLines(f))
            {
                no++;
                foreach (Match m in Wrapped.Matches(line)) add(Unescape(m.Groups[1].Value), Path.GetFileName(f) + ":" + no);
            }
        }
        // 2) Feste Namenslisten der Oberfläche (werden bei der Anzeige übersetzt)
        var arrays = new Regex(@"static readonly string\[,?\] (\w*(Names|Texts|Tabs|Labels|Short)|PadTable)\s*=([^;]*);", RegexOptions.Singleline);
        foreach (var f in Directory.GetFiles(Path.Combine(Src, "Runtime"), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(f);
            foreach (Match m in arrays.Matches(text)) if (m.Groups[1].Value != "MenuTabs")
                foreach (Match l in Literal.Matches(m.Groups[3].Value)) add(Unescape(l.Groups[1].Value), Path.GetFileName(f) + ":" + m.Groups[1].Value);
            if (f.EndsWith("InputMap.cs"))
            {
                int a = text.IndexOf("Dictionary<GameAction, string> Names", StringComparison.Ordinal);
                int b = text.IndexOf("};", a, StringComparison.Ordinal);
                foreach (Match l in Literal.Matches(text.Substring(a, b - a))) add(Unescape(l.Groups[1].Value), "InputMap.Names");
            }
        }
        // 3) Datentabellen, Erzählerzeilen, Radio
        foreach (var t in Loc.DataTexts()) add(t, "GameData");
        foreach (var l in Story.Lines) add(l.Text, "Story " + l.Id);
        foreach (var t in Story.Tracks) { add(t.Title, "Radio " + t.Id); add(t.UnlockHint, "Radio " + t.Id); }
        foreach (var s in IntroTimeline.Shots) foreach (var l in s.Lines) add(l, "Intro " + s.Id);
        foreach (var t in new[] { "Automatisch", "Spielstand 1", "Spielstand 2", "Spielstand 3" }) add(t, "GameApp.SlotName");
        foreach (var id in Rules.ToolIds) add(Rules.ToolName(id), "Rules.ToolName");
        // 4) Namen aus der Weltgenerierung (Lichtpunkte, Reparatur-, Öko-, Fundstück- und Aussichtspunkte)
        foreach (var p in GameData.PlanetOrder)
        {
            var l = WorldGen.Get(p);
            foreach (var z in l.Zones) add(z.Name, "Zone " + p);
            foreach (var list in new[] { l.Repairs, l.Eco, l.LoreSpots, l.Viewpoints, l.Shelters, l.Bots })
                foreach (var s in list) add(s.Name, "Spot " + p);
        }
        return req;
    }

    /// <summary>Kanonische deutsche Meldungen, die Server/Spiel zur Laufzeit zusammensetzen: jedes Textstück muss in einer Vorlage vorkommen.</summary>
    public static List<KeyValuePair<string, string>> MessageFragments()
    {
        var files = new List<string>();
        foreach (var d in new[] { "Core/Sim", "Core/Net", "Core/Save" }) files.AddRange(Directory.GetFiles(Path.Combine(Src, d), "*.cs"));
        files.Add(Path.Combine(Src, "Core/Data/GameData.cs"));
        foreach (var f in new[] { "Runtime/Game/GameApp.cs", "Runtime/Game/CameraRig.cs", "Runtime/Render/WorldView.cs", "Runtime/UI/HudHints.cs", "Runtime/Game/PlayerController.cs", "Runtime/Game/Settings.cs",
                                   "Runtime/Game/FeatureToasts.cs", "Runtime/Game/PlayerControllerFeatures.cs", "Runtime/UI/FeaturesUI.cs", "Runtime/Render/FeaturesView.cs" })
            files.Add(Path.Combine(Src, f));
        var res = new List<KeyValuePair<string, string>>();
        var skip = new Regex(@"Debug\.Log|^\s*//|^\s*///|Hash\.|throw new InvalidOperation|Log\?\.Invoke|LogWarning|Console\.");
        var badBefore = new Regex(@"(\bcase\s*|\.Set\(|\.Str\(|\.Int\(|\.Bool\(|\.Float\(|\.Obj\(|\.Arr\(|\.Strs\(|\.Has\(|ContainsKey\(|TryGetValue\(|==|!=|Stat\(|AddStat\(|Tech\w*\(|Get\(|Planet\(|Projects\[|\bL\(|Loc\.T\(|Loc\.F\()\s*$");
        foreach (var f in files)
        {
            int no = 0;
            bool story = f.EndsWith("Story.cs");
            foreach (var line in File.ReadLines(f))
            {
                no++;
                if (story || skip.IsMatch(line)) continue;
                if (f.EndsWith("GameData.cs")) continue; // Daten werden über DataTexts geprüft
                foreach (Match m in Literal.Matches(line))
                {
                    string t = Unescape(m.Groups[1].Value);
                    string before = line.Substring(0, m.Index);
                    if (badBefore.IsMatch(before)) continue;
                    if (!Regex.IsMatch(t, @"[a-zäöüß]{2}") || !(t.Contains(" ") || Regex.IsMatch(t, "[äöüßÄÖÜ]") || char.IsUpper(t.TrimStart()[0]))) continue;
                    if (Regex.IsMatch(t, @"^[\w.:/\-]+$") && !Regex.IsMatch(t, "[äöüßÄÖÜ]") && char.IsLower(t[0])) continue;
                    res.Add(new KeyValuePair<string, string>(t.Trim(), Path.GetFileName(f) + ":" + no));
                }
            }
        }
        return res;
    }

    /// <summary>Bezeichner und Formate, die wie Text aussehen, aber nie angezeigt werden.</summary>
    static readonly HashSet<string> NotText = new HashSet<string>
    {
        "RePlanet.", "AudioManager", "WorldView", "CameraRig", "PlayerController", "IntroDirector", "EndingDirector", "UIRoot", "MainCamera",
        "Buildings", "Base", "Backdrop", "TransportShip", "Planet_", "Terrain", "Water", "Dune", "GreenhouseRuin", "GreenhouseRestored",
        "BuildPadCursor", "BuildPreview", "RePlanet_", "RePlanet-Accept", "RePlanet-Read-", "RePlanet-Write-", "RePlanet-ClientWrite", "RePlanet-ClientRead",
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "Fotos", "Vorschau", "Menü", "Aussichtspunkt gespeichert",
        "Features", "FeatureLine", "Helfer_", "Space",
    };

    public static List<string> Missing()
    {
        var table = Loc.EnglishTable;
        var miss = new List<string>();
        var seen = new HashSet<string>();
        foreach (var kv in RequiredTexts())
            if (seen.Add(kv.Key) && Loc.TryTranslate(kv.Key, "en") == null) miss.Add("TEXT\t" + kv.Value + "\t" + kv.Key);
        var keys = table.Keys.ToList();
        foreach (var kv in MessageFragments())
        {
            if (kv.Key.Length < 2 || NotText.Contains(kv.Key) || !seen.Add("§" + kv.Key)) continue;
            if (Loc.TryTranslate(kv.Key, "en") != null) continue;
            if (keys.Any(k => k.Contains(kv.Key))) continue;
            miss.Add("FRAG\t" + kv.Value + "\t" + kv.Key);
        }
        return miss;
    }

    [Test]
    public static void Jeder_deutsche_Text_hat_eine_englische_Uebersetzung()
    {
        var miss = Missing();
        Assert.True(miss.Count == 0, miss.Count + " Texte ohne Übersetzung, z. B.: " + string.Join(" | ", miss.Take(8)));
    }

    [Test]
    public static void Uebersetzungen_haben_gleiche_Platzhalter_und_keine_Konflikte()
    {
        Assert.True(Loc.Conflicts.Count == 0 || Loc.EnglishTable != null && Loc.Conflicts.Count == 0, "Doppelte Schlüssel mit verschiedener Übersetzung: " + string.Join(" | ", Loc.Conflicts.Take(5)));
        foreach (var kv in Loc.EnglishTable)
        {
            Assert.True(!string.IsNullOrEmpty(kv.Value), "Leere Übersetzung: " + kv.Key);
            var a = Loc.PlaceholdersOf(kv.Key);
            var b = Loc.PlaceholdersOf(kv.Value);
            Assert.True(a.SetEquals(b), "Platzhalter verschieden: „" + kv.Key + "“ → „" + kv.Value + "“");
            Assert.Equal(kv.Key.StartsWith(" "), kv.Value.StartsWith(" "), "Führendes Leerzeichen gleich: „" + kv.Key + "“");
            Assert.Equal(kv.Key.EndsWith(" "), kv.Value.EndsWith(" "), "Folgendes Leerzeichen gleich: „" + kv.Key + "“");
        }
    }

    [Test]
    public static void Vorlagen_uebersetzen_zusammengesetzte_Meldungen()
    {
        try
        {
            Loc.Lang = "en";
            Assert.Equal("Continue", Loc.T("Fortsetzen"), "Exakter Eintrag");
            Assert.Equal("  Continue ", Loc.T("  Fortsetzen "), "Leerraum bleibt erhalten");
            Assert.Equal("<b>Continue</b>", Loc.T("<b>Fortsetzen</b>"), "Äußeres Tag bleibt erhalten");
            Assert.Equal("Unbekannt 123", Loc.T("Unbekannt 123"), "Unbekanntes bleibt unverändert");
            // Meldung des Servers mit Zahlen und Namen aus den Datentabellen
            var tt = GameData.Trash["kuehlschrank"];
            string de = Rules.CollectCheck(new WorldState(), Rules.FromDyn(new DynObj { Id = "d1", Type = "kuehlschrank", Pos = new V3(0, 0, 0) }), "grab", new V3(0, 0, 0), 0);
            string en = Loc.T(de);
            Assert.True(en != de, "Ablehnungsgrund wird übersetzt: " + de);
            Assert.True(!Regex.IsMatch(en, "[äöüß]"), "Ergebnis ohne deutsche Reste: " + en);
            Assert.Equal("12,345", Loc.Num(12345), "Tausendertrennzeichen Englisch");
            Loc.ApplyToData();
            Assert.True(GameData.Materials["glas"].Name == Loc.T("Glas") && GameData.Materials["glas"].Name != "Glas", "Datentabellen übersetzt");
        }
        finally
        {
            Loc.Lang = "de";
            Loc.ApplyToData();
        }
        Assert.Equal("Glas", GameData.Materials["glas"].Name, "Zurück auf Deutsch");
        Assert.Equal("12.345", Loc.Num(12345), "Tausendertrennzeichen Deutsch");
    }
}
