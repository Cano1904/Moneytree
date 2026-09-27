using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RePlanet.Core
{
    /// <summary>JSON-Objekt mit typsicheren Lesehilfen. Wird für Spielstände und Netzwerk verwendet.</summary>
    public class JObj : Dictionary<string, object>
    {
        public JObj() : base(StringComparer.Ordinal) { }

        public JObj Set(string key, object value) { this[key] = value; return this; }

        public bool Has(string key) { return ContainsKey(key) && this[key] != null; }

        public string Str(string key, string def = null)
        {
            object v;
            if (TryGetValue(key, out v) && v != null) return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
            return def;
        }

        public double Num(string key, double def = 0)
        {
            object v;
            if (TryGetValue(key, out v) && v != null) return Json.ToDouble(v, def);
            return def;
        }

        public int Int(string key, int def = 0) { return Has(key) ? (int)Math.Round(Num(key, def)) : def; }
        public long Long(string key, long def = 0) { return Has(key) ? (long)Math.Round(Num(key, def)) : def; }
        public float Float(string key, float def = 0f) { return Has(key) ? (float)Num(key, def) : def; }

        public bool Bool(string key, bool def = false)
        {
            object v;
            if (TryGetValue(key, out v) && v is bool) return (bool)v;
            return def;
        }

        public JObj Obj(string key)
        {
            object v;
            if (TryGetValue(key, out v)) return v as JObj;
            return null;
        }

        public List<object> Arr(string key)
        {
            object v;
            if (TryGetValue(key, out v)) return v as List<object>;
            return null;
        }

        public float[] Floats(string key)
        {
            var a = Arr(key);
            if (a == null) return null;
            var r = new float[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = (float)Json.ToDouble(a[i], 0);
            return r;
        }

        public List<string> Strs(string key)
        {
            var a = Arr(key);
            var r = new List<string>();
            if (a == null) return r;
            foreach (var o in a) if (o is string) r.Add((string)o);
            return r;
        }

        public List<int> Ints(string key)
        {
            var a = Arr(key);
            var r = new List<int>();
            if (a == null) return r;
            foreach (var o in a) r.Add((int)Math.Round(Json.ToDouble(o, 0)));
            return r;
        }
    }

    /// <summary>Kleiner, abhängigkeitsfreier JSON-Leser/-Schreiber (läuft in Unity und .NET).</summary>
    public static class Json
    {
        public static double ToDouble(object v, double def)
        {
            if (v == null) return def;
            if (v is double) return (double)v;
            if (v is float) return (float)v;
            if (v is int) return (int)v;
            if (v is long) return (long)v;
            if (v is bool) return ((bool)v) ? 1 : 0;
            double d;
            if (v is string && double.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        /// <summary>Rundet für kompakte Übertragung.</summary>
        public static double R(float v, int decimals = 2)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0;
            return Math.Round(v, decimals);
        }

        public static List<object> Arr(params object[] items) { return new List<object>(items); }

        // ------------------------------------------------------------ Schreiben
        public static string Write(object value)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return;
            }
            if (v is float)
            {
                float f = (float)v;
                if (float.IsNaN(f) || float.IsInfinity(f)) f = 0;
                sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); return;
            }
            if (v is double)
            {
                double d = (double)v;
                if (double.IsNaN(d) || double.IsInfinity(d)) d = 0;
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
            }
            var dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    WriteValue(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            var list = v as IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ Lesen
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON ist leer");
            int i = 0;
            var v = ParseValue(text, ref i, 0);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("Unerwartete Zeichen nach JSON-Ende bei " + i);
            return v;
        }

        public static JObj ParseObj(string text)
        {
            var o = Parse(text) as JObj;
            if (o == null) throw new FormatException("JSON-Objekt erwartet");
            return o;
        }

        public static bool TryParseObj(string text, out JObj obj)
        {
            try { obj = ParseObj(text); return true; }
            catch { obj = null; return false; }
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static object ParseValue(string s, ref int i, int depth)
        {
            if (depth > 64) throw new FormatException("JSON zu tief verschachtelt");
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("Unerwartetes JSON-Ende");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var o = new JObj();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return o; }
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != '"') throw new FormatException("Schlüssel erwartet bei " + i);
                    string key = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' erwartet bei " + i);
                    i++;
                    o[key] = ParseValue(s, ref i, depth + 1);
                    SkipWs(s, ref i);
                    if (i >= s.Length) throw new FormatException("Unerwartetes Ende im Objekt");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return o; }
                    throw new FormatException("',' oder '}' erwartet bei " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var a = new List<object>();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (true)
                {
                    a.Add(ParseValue(s, ref i, depth + 1));
                    SkipWs(s, ref i);
                    if (i >= s.Length) throw new FormatException("Unerwartetes Ende im Array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return a; }
                    throw new FormatException("',' oder ']' erwartet bei " + i);
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' && Match(s, i, "true")) { i += 4; return true; }
            if (c == 'f' && Match(s, i, "false")) { i += 5; return false; }
            if (c == 'n' && Match(s, i, "null")) { i += 4; return null; }
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int start = i;
                i++;
                while (i < s.Length)
                {
                    char d = s[i];
                    if ((d >= '0' && d <= '9') || d == '.' || d == 'e' || d == 'E' || d == '+' || d == '-') i++;
                    else break;
                }
                double num;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out num))
                    throw new FormatException("Ungültige Zahl bei " + start);
                return num;
            }
            throw new FormatException("Unerwartetes Zeichen '" + c + "' bei " + i);
        }

        static bool Match(string s, int i, string word)
        {
            return i + word.Length <= s.Length && string.CompareOrdinal(s, i, word, 0, word.Length) == 0;
        }

        static string ParseString(string s, ref int i)
        {
            i++; // "
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Ungültiges Unicode-Escape");
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: throw new FormatException("Ungültiges Escape \\" + e);
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("Nicht beendete Zeichenkette");
        }
    }
}
