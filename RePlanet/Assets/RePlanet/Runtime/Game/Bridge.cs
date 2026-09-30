using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public enum ToastKind { Info, Success, Warning, Error, Story }

    public class Toast
    {
        public string Text;
        public ToastKind Kind;
        public float Created, Duration;
        // ---- Darstellung (vom HUD gesetzt, je nach Einstellung „Hinweise“)
        /// <summary>Vom HUD bereits ausgewertet.</summary>
        public bool Seen;
        /// <summary>Wird in der aktuellen Hinweisstufe nicht gezeigt (unterdrückt oder in eine andere Meldung eingerechnet).</summary>
        public bool Hidden;
        /// <summary>Gekürzter Anzeigetext (null = <see cref="Text"/>).</summary>
        public string Shown;
        /// <summary>Gleichartige Meldungen werden zusammengefasst (z. B. „credits“ → Summe).</summary>
        public string MergeKey;
        public long Sum;
        public int Count = 1;
    }

    /// <summary>
    /// Gemeinsamer Zustand zwischen Spiellogik-Darstellung (PlayerController/WorldView) und Oberfläche (UIRoot).
    /// Gameplay schreibt, UI liest. So bleiben beide Seiten unabhängig voneinander.
    /// </summary>
    public static class Hud
    {
        /// <summary>Aktuelle Interaktion, z. B. „[E] Aufheben: Glasflasche“.</summary>
        public static string Prompt;
        /// <summary>Warum die aktuelle Aktion nicht geht (konkreter Grund).</summary>
        public static string Blocked;
        /// <summary>Kurzform für das ruhige HUD: Tastenbezeichnung (ohne Klammern, null = keine Taste) und ein Wort, z. B. „E“ + „Werkstatt“.</summary>
        public static string PromptKey, PromptWord;
        /// <summary>Taste muss gehalten werden.</summary>
        public static bool PromptHold;
        /// <summary>Ankerpunkt der Interaktion in der Welt (über dem Objekt); null = unten in der Mitte.</summary>
        public static Vector3? PromptAt;
        /// <summary>Kurzform von <see cref="Blocked"/> (wenige Wörter) und ihr Ankerpunkt.</summary>
        public static string BlockedShort;
        public static Vector3? BlockedAt;
        /// <summary>Fortschritt 0..1 einer laufenden Aktion (Schneiden, Tauen …), sonst −1.</summary>
        public static float Progress = -1f;
        public static string ProgressLabel;
        /// <summary>Aufladung des Magneten 0..1, sonst −1.</summary>
        public static float MagnetCharge = -1f;
        /// <summary>Station in Reichweite (sell, workshop, storage, sort, trader, disposal, contracts, ship, garage, build) oder null.</summary>
        public static string NearStation;
        public static bool InVehicle;
        public static string VehicleId;
        /// <summary>Fahrzeug steckt fest → Hinweis auf Zurücksetzen.</summary>
        public static bool VehicleStuck;
        /// <summary>Richtung/Entfernung zum nächsten Unterschlupf (für Nacht- und Sturmwarnung).</summary>
        public static Vector3 ShelterPos;
        public static float ShelterDist = -1f;
        /// <summary>Kompassrichtung der Kamera (Grad), für die Kartenanzeige.</summary>
        public static float CameraYaw;
        public static bool Swimming, Diving;

        public static readonly List<Toast> Toasts = new List<Toast>();
        public static string Subtitle;
        public static float SubtitleUntil;
        public static string SubtitleSpeaker;

        public static void Show(string text, ToastKind kind = ToastKind.Info, float duration = 3.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Gleiche Meldung nicht stapeln
            foreach (var t in Toasts)
                if (t.Text == text && t.MergeKey == null && Time.unscaledTime - t.Created < t.Duration) { t.Created = Time.unscaledTime; t.Count++; return; }
            Toasts.Add(new Toast { Text = text, Kind = kind, Created = Time.unscaledTime, Duration = duration });
            if (Toasts.Count > 6) Toasts.RemoveAt(0);
        }

        /// <summary>Setzt den Interaktionshinweis in Lang- und Kurzform.</summary>
        public static void SetPrompt(string full, string key, string word, Vector3? at = null, bool hold = false)
        {
            Prompt = Loc.T(full); PromptKey = key; PromptWord = Loc.T(word); PromptAt = at; PromptHold = hold;
        }

        /// <summary>Setzt den Grund, warum etwas nicht geht, in Lang- und Kurzform.</summary>
        public static void SetBlocked(string full, string shortText, Vector3? at = null)
        {
            Blocked = Loc.T(full); BlockedShort = Loc.T(shortText); BlockedAt = at;
        }

        /// <summary>Untertitel auch bei ausgeschalteten Untertiteln zeigen (Erzählerzeile ohne Aufnahme).</summary>
        public static bool SubtitleAlways;

        public static void Say(string text, float duration, string speaker = null, bool always = false)
        {
            Subtitle = text;
            SubtitleSpeaker = speaker;
            SubtitleUntil = Time.unscaledTime + duration;
            SubtitleAlways = always;
        }

        public static void ClearTransient()
        {
            Prompt = null; Blocked = null; PromptKey = null; PromptWord = null; PromptAt = null; PromptHold = false; BlockedShort = null; BlockedAt = null;
            Progress = -1f; ProgressLabel = null; MagnetCharge = -1f; NearStation = null; VehicleStuck = false;
        }
    }

    public enum UIScreen
    {
        None, MainMenu, NewGame, PlanetSelect, Saves, Settings, Coop, Pause, Menu, Map, Photo, Loading, Intro, Ending, Message, Travel, Credits
    }

    /// <summary>Welcher Bildschirm offen ist. Gameplay-Eingaben werden gesperrt, solange ein blockierender Bildschirm offen ist.</summary>
    public static class UIState
    {
        public static UIScreen Screen = UIScreen.None;
        /// <summary>Reiter im Spielmenü: inventory, missions, map, workshop, storage, archive, robot, coop.</summary>
        public static string MenuTab = "inventory";
        /// <summary>Stationskontext beim Öffnen (z. B. „sell“ öffnet direkt den Lager/Verkauf-Reiter).</summary>
        public static string Station;
        public static string MessageTitle, MessageText;
        public static UIScreen ReturnTo = UIScreen.MainMenu;

        public static bool BlocksGameplay
        {
            get { return Screen != UIScreen.None && Screen != UIScreen.Photo; }
        }

        public static void Open(UIScreen s) { Screen = s; }

        public static void Message(string title, string text, UIScreen returnTo)
        {
            MessageTitle = title; MessageText = text; ReturnTo = returnTo; Screen = UIScreen.Message;
        }
    }

    /// <summary>Fotomodus: UI setzt die Werte, CameraRig setzt sie um.</summary>
    public static class PhotoMode
    {
        public static bool Active;
        public static bool HideHud = true;
        public static float Fov = 55f;
        public static float Roll;
        /// <summary>Hochformat-Ausschnitt 9:16 für kurze Videos.</summary>
        public static bool Portrait;
        /// <summary>Zeigt den Ausgangszustand (Müll vor dem Aufräumen) für Vorher-nachher-Bilder.</summary>
        public static bool ShowBefore;
        public static bool RequestCapture;
        public static string LastSavedPath;
        public static float Exposure = 1f;
        /// <summary>Aussichtspunkt, zu dem gesprungen werden soll (Spot-Id) – wird von CameraRig abgearbeitet.</summary>
        public static string JumpToViewpoint;
    }

    /// <summary>Bauansicht: UI wählt Gebäude/Drehung, WorldView zeigt die Vorschau und prüft die Platzierung.</summary>
    public static class BuildMode
    {
        public static bool Active;
        public static string Type;
        public static int Rot;
        /// <summary>Id eines Gebäudes, das umgesetzt wird (−1 = neu bauen).</summary>
        public static int MoveId = -1;
        public static int Gx, Gz;
        public static bool HasCursor, Valid;
        public static string Reason;
        public static int HoverBuildingId = -1;
        public static bool RequestPlace, RequestDemolish;
        /// <summary>Controller: Zielpunkt auf der Baufläche (Weltkoordinaten), per linkem Stick bewegt.</summary>
        public static Vector3 PadCursor;
        public static bool PadCursorInit;
        /// <summary>Kategorie-Filter der Bauleiste (null = alle).</summary>
        public static string Category;
    }
}
