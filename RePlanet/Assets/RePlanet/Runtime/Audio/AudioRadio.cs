using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Radio im Spiel: spielt statt der Planetenmusik Stücke aus <see cref="Story.Tracks"/> (vorhandene Musikstücke mit eigener
    /// Stem-Mischung; freigeschaltet durch Fortschritt, gespeichert im Spielstand). Zwischen zwei Stücken eine kurze
    /// synthetische Senderkennung (<see cref="Synth.RadioJingles"/>). Sender: „Radio Zweite Chance“ (alle freien Stücke)
    /// und je Planet ein eigener Sender, sobald dort ein Stück frei ist. Lautstärke = Musik. Rein lokal (jeder Spieler hört sein Radio).
    /// </summary>
    public partial class AudioManager
    {
        bool radioOn;
        string radioStation = "all";
        RadioTrack radioTrack;
        int radioPhase;            // 0 aus, 1 Ansage (Kennung, nächstes Stück lädt), 2 Stück läuft
        float radioPhaseAt, radioTrackStart, radioTrackEnd;
        bool radioJinglePlayed;
        int radioJingleIdx;
        readonly List<string> radioHistory = new List<string>();
        AudioSource radioSrc;

        // ================================================================== Öffentliche Schnittstelle
        public static bool RadioOn { get { return I != null && I.radioOn; } }
        /// <summary>Laufendes (oder gleich folgendes) Stück; null, wenn das Radio aus ist.</summary>
        public static RadioTrack RadioCurrent { get { return I != null && I.radioOn ? I.radioTrack : null; } }
        public static bool RadioAnnouncing { get { return I != null && I.radioOn && I.radioPhase == 1; } }
        public static string RadioStation { get { return I != null ? I.radioStation : "all"; } }
        /// <summary>Fortschritt des laufenden Stücks 0 … 1 (−1 = keins).</summary>
        public static float RadioProgress
        {
            get
            {
                if (I == null || !I.radioOn || I.radioPhase != 2) return -1f;
                float len = Mathf.Max(1f, I.radioTrackEnd - I.radioTrackStart);
                return Mathf.Clamp01((Time.unscaledTime - I.radioTrackStart) / len);
            }
        }

        /// <summary>Anzeigename eines Senders.</summary>
        public static string StationName(string station)
        {
            if (station == null || station == "all") return Loc.T("Radio Zweite Chance");
            PlanetDef pd;
            return GameData.Planets.TryGetValue(station, out pd) ? pd.Name + " FM" : station;
        }

        /// <summary>Verfügbare Sender: „all“ und jeder Planet mit mindestens einem freien eigenen Stück.</summary>
        public static List<string> RadioStations(WorldState w)
        {
            var l = new List<string> { "all" };
            if (w == null) return l;
            foreach (var p in GameData.PlanetOrder)
                foreach (var t in Story.Tracks)
                    if (t.Planet == p && w.RadioUnlocked.Contains(t.Id)) { l.Add(p); break; }
            return l;
        }

        public static void RadioToggle()
        {
            if (I == null) return;
            try
            {
                if (I.radioOn) I.RadioStop();
                else I.RadioStart(null);
            }
            catch (Exception e) { Debug.LogWarning("[Radio] " + e.Message); }
        }

        public static void RadioSetStation(string station)
        {
            if (I == null) return;
            try
            {
                I.radioStation = string.IsNullOrEmpty(station) ? "all" : station;
                var app = GameApp.I;
                if (app != null && app.Settings != null && app.Settings.RadioStation != I.radioStation) { app.Settings.RadioStation = I.radioStation; app.Settings.Save(); }
                I.RadioStart(null);
            }
            catch (Exception e) { Debug.LogWarning("[Radio] " + e.Message); }
        }

        /// <summary>Bestimmtes Stück spielen (nach kurzer Ansage).</summary>
        public static void RadioPlay(string trackId)
        {
            if (I == null) return;
            RadioTrack t;
            if (trackId != null && Story.TrackById.TryGetValue(trackId, out t)) { try { I.RadioStart(t); } catch (Exception e) { Debug.LogWarning("[Radio] " + e.Message); } }
        }

        public static void RadioNext()
        {
            if (I == null || !I.radioOn) return;
            try { I.RadioAnnounce(I.PickRadioTrack()); }
            catch (Exception e) { Debug.LogWarning("[Radio] " + e.Message); }
        }

        // ================================================================== Ablauf
        void RadioStart(RadioTrack wish)
        {
            var app = GameApp.I;
            if (app == null || !app.InGame) return;
            if (!radioOn)
            {
                radioOn = true;
                if (app.Settings != null && !string.IsNullOrEmpty(app.Settings.RadioStation)) radioStation = app.Settings.RadioStation;
                if (!RadioStations(app.W).Contains(radioStation)) radioStation = "all";
            }
            RadioAnnounce(wish ?? PickRadioTrack());
        }

        void RadioStop()
        {
            radioOn = false;
            radioPhase = 0;
            radioTrack = null;
            if (radioSrc != null) radioSrc.Stop();
        }

        void RadioAnnounce(RadioTrack next)
        {
            if (next == null) { RadioStop(); return; }
            radioTrack = next;
            radioPhase = 1;
            radioPhaseAt = Time.unscaledTime;
            radioJinglePlayed = false;
            radioJingleIdx = (radioJingleIdx + 1) % Synth.RadioJingles.Length;
            Request("music:" + next.Piece, -5, true);
            foreach (var j in Synth.RadioJingles) Sfx(j);
        }

        RadioTrack PickRadioTrack()
        {
            var app = GameApp.I;
            if (app == null || app.W == null) return null;
            var list = Story.Unlocked(app.W, radioStation == "all" ? null : radioStation);
            if (list.Count == 0) list = Story.Unlocked(app.W);
            if (list.Count == 0) return null;
            var pool = new List<RadioTrack>();
            int avoid = Mathf.Min(2, list.Count - 1);
            foreach (var t in list)
            {
                int h = radioHistory.LastIndexOf(t.Id);
                if (avoid > 0 && h >= 0 && h >= radioHistory.Count - avoid) continue;
                if (radioTrack != null && t == radioTrack && list.Count > 1) continue;
                pool.Add(t);
            }
            if (pool.Count == 0) pool = list;
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            radioHistory.Add(pick.Id);
            if (radioHistory.Count > 8) radioHistory.RemoveAt(0);
            return pick;
        }

        void UpdateRadio(float dt)
        {
            if (radioSrc == null) { radioSrc = NewSource("Radio", false, 8); radioSrc.loop = false; }
            radioSrc.volume = MusicVolume() * focusGain * 0.9f;
            if (!radioOn) return;
            var app = GameApp.I;
            if (app == null || !app.InGame) { RadioStop(); return; }
            if (radioTrack == null || !app.W.RadioUnlocked.Contains(radioTrack.Id)) { RadioAnnounce(PickRadioTrack()); if (!radioOn) return; }
            float now = Time.unscaledTime;
            if (radioPhase == 1)
            {
                // Planetenmusik blendet aus, dann Kennung; danach startet das Stück, sobald es geladen ist
                if (!radioJinglePlayed && now - radioPhaseAt > 0.7f)
                {
                    var clip = Sfx(Synth.RadioJingles[radioJingleIdx]);
                    if (clip != null)
                    {
                        radioSrc.clip = clip;
                        radioSrc.Play();
                        radioJinglePlayed = true;
                        Hud.Show("♪ " + StationName(radioStation) + " – " + Loc.T(radioTrack.Title), ToastKind.Info, 4f);
                    }
                    else if (now - radioPhaseAt > 3f) radioJinglePlayed = true; // Kennung fehlt noch – nicht warten
                }
                bool jingleDone = radioJinglePlayed && !radioSrc.isPlaying;
                MusicClips mc;
                if (jingleDone && music.TryGetValue(radioTrack.Piece, out mc))
                {
                    radioPhase = 2;
                    int loops = Mathf.Max(2, Mathf.RoundToInt(150f / Mathf.Max(10f, mc.Length)));
                    radioTrackStart = now;
                    radioTrackEnd = now + loops * mc.Length - 2.5f; // Ausblenden (≈ 2,5 s) endet mit dem Loop
                }
                else if (jingleDone) Request("music:" + radioTrack.Piece, -5, true);
            }
            else if (radioPhase == 2 && now >= radioTrackEnd) RadioAnnounce(PickRadioTrack());
        }

        /// <summary>Musikwunsch des Radios: true = Radio bestimmt (want = Stück oder null für Stille während der Ansage).</summary>
        bool RadioOverride(out string want)
        {
            want = null;
            if (!radioOn || radioTrack == null) return false;
            want = radioPhase == 2 ? radioTrack.Piece : null;
            return true;
        }

        /// <summary>Stem-Mischung des Radios für diesen Musiksatz (null = nicht das Radiostück).</summary>
        float[] RadioMixFor(MusicClips mc)
        {
            if (!radioOn || radioPhase != 2 || radioTrack == null || mc == null || mc.Id != radioTrack.Piece) return null;
            return radioTrack.Mix;
        }
    }
}
