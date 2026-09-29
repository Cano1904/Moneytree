using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RePlanet.Core
{
    /// <summary>
    /// Lokalisierung. Standardsprache ist Deutsch; der deutsche Text ist zugleich der Schlüssel.
    /// Weitere Sprachen ergänzen eine Tabelle „Deutsch → Übersetzung“ (Englisch: <c>LocEn.cs</c>). Fehlende Einträge fallen auf Deutsch zurück.
    /// <list type="bullet">
    /// <item><see cref="T"/> übersetzt einen fertigen Text: exakt, sonst über <b>Vorlagen</b> mit Platzhaltern
    /// („{0} ist jetzt erreichbar!“). So lassen sich auch Meldungen übersetzen, die der Server (Core) auf Deutsch
    /// zusammensetzt – etwa Ablehnungsgründe mit Zahlen. Eingesetzte Werte werden ihrerseits übersetzt (Namen aus den Datentabellen).</item>
    /// <item><see cref="F"/> formatiert eine Vorlage in der aktuellen Sprache.</item>
    /// <item>Führende/folgende Leerzeichen und ein äußeres Rich-Text-Tag (&lt;b&gt;, &lt;color&gt;) bleiben erhalten.</item>
    /// </list>
    /// Core-Code erzeugt immer deutsche Texte (serverautoritativ, Mitspieler können andere Sprachen haben);
    /// übersetzt wird erst bei der Anzeige.
    /// </summary>
    public static partial class Loc
    {
        static string lang = "de";
        /// <summary>Aktuelle Sprache („de“ oder „en“). Unbekannte Werte fallen auf Deutsch zurück.</summary>
        public static string Lang
        {
            get { return lang; }
            set
            {
                string v = Array.IndexOf(Languages, value) >= 0 ? value : "de";
                if (v == lang) return;
                lang = v;
                lock (cacheLock) cache.Clear();
            }
        }

        public static readonly string[] Languages = { "de", "en" };
        public static readonly string[] LanguageNames = { "Deutsch", "English" };
        public static bool English { get { return lang == "en"; } }

        // ------------------------------------------------------------ Tabellen
        static Dictionary<string, string> en;
        static List<Template> templates;
        static Dictionary<string, List<Template>> byPrefix;
        static List<Template> openStart;
        static readonly object buildLock = new object();

        class Template
        {
            public string Key, Value;
            public Regex Rx;
            public int[] Slots;   // Platzhalternummer je Fanggruppe
        }

        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[:,][^}]*)?\}", RegexOptions.Compiled);

        /// <summary>Alle englischen Einträge (Schlüssel = deutscher Text). Für Tests und Werkzeuge.</summary>
        public static IDictionary<string, string> EnglishTable { get { Ensure(); return en; } }

        /// <summary>Doppelte Schlüssel mit unterschiedlicher Übersetzung (Test: muss leer sein).</summary>
        public static readonly List<string> Conflicts = new List<string>();

        static void Ensure()
        {
            if (en != null) return;
            lock (buildLock)
            {
                if (en != null) return;
                var d = new Dictionary<string, string>(4096, StringComparer.Ordinal);
                var add = new Action<string, string>((k, v) =>
                {
                    string old;
                    if (d.TryGetValue(k, out old) && old != v) Conflicts.Add(k);
                    d[k] = v;
                });
                FillEnglish(add);
                var tl = new List<Template>();
                var bp = new Dictionary<string, List<Template>>(StringComparer.Ordinal);
                var open = new List<Template>();
                foreach (var kv in d)
                {
                    if (kv.Key.IndexOf('{') < 0 || !Placeholder.IsMatch(kv.Key)) continue;
                    var t = MakeTemplate(kv.Key, kv.Value);
                    if (t == null) continue;
                    tl.Add(t);
                    string pre = LiteralPrefix(kv.Key);
                    if (pre.Length >= 2)
                    {
                        string k2 = pre.Substring(0, 2);
                        List<Template> l;
                        if (!bp.TryGetValue(k2, out l)) { l = new List<Template>(); bp[k2] = l; }
                        l.Add(t);
                    }
                    else open.Add(t);
                }
                // Längere (spezifischere) Vorlagen zuerst
                Comparison<Template> bySpec = (a, b) => LiteralLength(b.Key).CompareTo(LiteralLength(a.Key));
                foreach (var l in bp.Values) l.Sort(bySpec);
                open.Sort(bySpec);
                templates = tl; byPrefix = bp; openStart = open;
                en = d;
            }
        }

        static string LiteralPrefix(string key)
        {
            var m = Placeholder.Match(key);
            return m.Success ? key.Substring(0, m.Index) : key;
        }

        static int LiteralLength(string key) { return Placeholder.Replace(key, "").Length; }

        static Template MakeTemplate(string key, string value)
        {
            var sb = new StringBuilder("^");
            var slots = new List<int>();
            int last = 0;
            foreach (Match m in Placeholder.Matches(key))
            {
                sb.Append(Regex.Escape(key.Substring(last, m.Index - last)));
                sb.Append("(.+?)");
                slots.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                last = m.Index + m.Length;
            }
            sb.Append(Regex.Escape(key.Substring(last)));
            sb.Append('$');
            try
            {
                return new Template { Key = key, Value = value, Rx = new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), Slots = slots.ToArray() };
            }
            catch (ArgumentException) { return null; }
        }

        // ------------------------------------------------------------ Übersetzen
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly object cacheLock = new object();

        /// <summary>Übersetzt einen deutschen Text in die aktuelle Sprache (unbekannte Texte bleiben unverändert).</summary>
        public static string T(string de)
        {
            if (lang == "de" || string.IsNullOrEmpty(de)) return de;
            string r;
            lock (cacheLock) if (cache.TryGetValue(de, out r)) return r;
            Ensure();
            r = Translate(de, 0) ?? de;
            lock (cacheLock)
            {
                if (cache.Count > 8192) cache.Clear();
                cache[de] = r;
            }
            return r;
        }

        /// <summary>Formatiert eine (deutsche) Vorlage in der aktuellen Sprache: F("{0} Credits", n).</summary>
        public static string F(string template, params object[] args)
        {
            string t = T(template);
            try { return string.Format(CultureInfo.InvariantCulture, t, args); }
            catch (FormatException)
            {
                try { return string.Format(CultureInfo.InvariantCulture, template, args); }
                catch (FormatException) { return template; }
            }
        }

        /// <summary>Wie <see cref="T"/>, liefert aber null, wenn nichts gefunden wurde (für Tests).</summary>
        public static string TryTranslate(string de, string language)
        {
            if (language != "en" || string.IsNullOrEmpty(de)) return null;
            Ensure();
            return Translate(de, 0);
        }

        static string Translate(string s, int depth)
        {
            string r;
            if (en.TryGetValue(s, out r)) return r;
            // Leerraum am Rand erhalten
            int a = 0, b = s.Length;
            while (a < b && char.IsWhiteSpace(s[a])) a++;
            while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
            if (a == b) return null;
            if (a > 0 || b < s.Length)
            {
                var inner = Translate(s.Substring(a, b - a), depth);
                return inner == null ? null : s.Substring(0, a) + inner + s.Substring(b);
            }
            // Äußeres Rich-Text-Tag (<b>…</b>, <color=#…>…</color>)
            if (s[0] == '<' && s[s.Length - 1] == '>')
            {
                int open = s.IndexOf('>');
                int close = s.LastIndexOf("</", StringComparison.Ordinal);
                if (open > 0 && close > open && s.IndexOf('<', open + 1) == close)
                {
                    var inner = Translate(s.Substring(open + 1, close - open - 1), depth);
                    if (inner != null) return s.Substring(0, open + 1) + inner + s.Substring(close);
                }
            }
            if (depth > 3) return null;
            // Vorlagen
            List<Template> l;
            if (s.Length >= 2 && byPrefix.TryGetValue(s.Substring(0, 2), out l))
            {
                r = TryTemplates(l, s, depth);
                if (r != null) return r;
            }
            return TryTemplates(openStart, s, depth);
        }

        static string TryTemplates(List<Template> list, string s, int depth)
        {
            foreach (var t in list)
            {
                var m = t.Rx.Match(s);
                if (!m.Success) continue;
                int max = 0;
                foreach (var n in t.Slots) if (n > max) max = n;
                var args = new object[max + 1];
                for (int i = 0; i < args.Length; i++) args[i] = "";
                for (int g = 0; g < t.Slots.Length; g++)
                {
                    string v = m.Groups[g + 1].Value;
                    args[t.Slots[g]] = Translate(v, depth + 1) ?? v;
                }
                try { return string.Format(CultureInfo.InvariantCulture, StripFormats(t.Value), args); }
                catch (FormatException) { return null; }
            }
            return null;
        }

        /// <summary>Formatangaben („{0:0.0}“) gelten für Zahlen; beim Rückübersetzen sind die Werte schon Text.</summary>
        static string StripFormats(string v) { return Placeholder.Replace(v, m => "{" + m.Groups[1].Value + "}"); }

        /// <summary>Platzhalternummern eines Textes (für Tests: Deutsch und Übersetzung müssen übereinstimmen).</summary>
        public static SortedSet<int> PlaceholdersOf(string s)
        {
            var set = new SortedSet<int>();
            if (s == null) return set;
            foreach (Match m in Placeholder.Matches(s)) set.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return set;
        }

        /// <summary>Zahl mit Tausendertrennzeichen der aktuellen Sprache (Deutsch „12.345“, Englisch „12,345“).</summary>
        public static string Num(long v)
        {
            bool neg = v < 0;
            string d = (neg ? -v : v).ToString(CultureInfo.InvariantCulture);
            char sep = English ? ',' : '.';
            if (d.Length <= 3) return neg ? "-" + d : d;
            var sb = new StringBuilder(d.Length + d.Length / 3 + 1);
            if (neg) sb.Append('-');
            int first = d.Length % 3;
            if (first > 0) sb.Append(d, 0, first);
            for (int i = first; i < d.Length; i += 3)
            {
                if (sb.Length > (neg ? 1 : 0)) sb.Append(sep);
                sb.Append(d, i, 3);
            }
            return sb.ToString();
        }
    }
}
