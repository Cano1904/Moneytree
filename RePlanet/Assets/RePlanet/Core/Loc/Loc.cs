using System.Collections.Generic;

namespace RePlanet.Core
{
    /// <summary>
    /// Lokalisierung. Standardsprache ist Deutsch; der deutsche Text ist zugleich der Schlüssel.
    /// Weitere Sprachen ergänzen eine Tabelle „Deutsch → Übersetzung“. Fehlende Einträge fallen auf Deutsch zurück.
    /// </summary>
    public static class Loc
    {
        public static string Lang = "de";
        public static readonly string[] Languages = { "de", "en" };
        public static readonly string[] LanguageNames = { "Deutsch", "English (teilweise)" };

        static readonly Dictionary<string, string> en = new Dictionary<string, string>
        {
            { "Fortsetzen", "Continue" }, { "Neues Spiel", "New Game" }, { "Koop", "Co-op" }, { "Spielstände", "Saves" },
            { "Einstellungen", "Settings" }, { "Beenden", "Quit" }, { "Zurück", "Back" }, { "Speichern", "Save" }, { "Laden", "Load" },
            { "Hauptmenü", "Main Menu" }, { "Pause", "Pause" }, { "Grafik", "Graphics" }, { "Audio", "Audio" }, { "Steuerung", "Controls" },
            { "Barrierefreiheit", "Accessibility" }, { "Sprache", "Language" }, { "Inventar", "Inventory" }, { "Aufträge", "Missions" },
            { "Karte", "Map" }, { "Werkstatt", "Workshop" }, { "Lager", "Storage" }, { "Archiv", "Archive" }, { "Roboter", "Robot" },
            { "Bauen", "Build" }, { "Fotomodus", "Photo Mode" }, { "Credits", "Credits" }, { "Energie", "Energy" }, { "Ladung", "Load" },
            { "Werkzeug", "Tool" }, { "Ziel", "Objective" }, { "Qualität", "Quality" }, { "Auflösung", "Resolution" }, { "Vollbild", "Fullscreen" },
            { "Schatten", "Shadows" }, { "Kantenglättung", "Anti-aliasing" }, { "Sichtweite", "View distance" }, { "Render-Skalierung", "Render scale" },
            { "Partikel", "Particles" }, { "Bildrate begrenzen", "Frame rate limit" }, { "Gesamtlautstärke", "Master volume" }, { "Musik", "Music" },
            { "Effekte", "Effects" }, { "Umgebung", "Ambience" }, { "Oberfläche", "Interface" }, { "Untertitel", "Subtitles" },
            { "Textgröße", "Text size" }, { "Kamerawackeln", "Camera shake" }, { "Kameraempfindlichkeit", "Camera sensitivity" },
            { "Y-Achse umkehren", "Invert Y axis" }, { "Aktionen halten statt umschalten", "Hold actions instead of toggle" },
            { "Sitzung erstellen", "Host session" }, { "Sitzung beitreten", "Join session" }, { "Sitzungscode", "Session code" },
            { "Adresse", "Address" }, { "Spielername", "Player name" }, { "Verbinden", "Connect" }, { "Einladung kopieren", "Copy invite" },
            { "Aus", "Off" }, { "An", "On" }, { "Niedrig", "Low" }, { "Mittel", "Medium" }, { "Hoch", "High" }, { "Ultra", "Ultra" },
            { "Intro überspringen", "Skip intro" }, { "Gedrückt halten zum Überspringen", "Hold to skip" }, { "Verkaufen", "Sell" }, { "Kaufen", "Buy" },
            { "Einlagern", "Store" }, { "Sortieren", "Sort" }, { "Entsorgen", "Dispose" }, { "Reisen", "Travel" }, { "Tasten belegen", "Key bindings" },
        };

        public static string T(string de)
        {
            if (Lang == "de" || de == null) return de;
            string r;
            if (Lang == "en" && en.TryGetValue(de, out r)) return r;
            return de;
        }
    }
}
