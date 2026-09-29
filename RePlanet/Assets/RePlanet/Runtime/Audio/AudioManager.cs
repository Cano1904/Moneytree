using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Klangsteuerung von RE:PLANET. Alle Klänge stammen aus <see cref="Synth"/> (prozedural, keine Asset-Dateien):
    /// <list type="bullet">
    /// <item>Erzeugung auf Hintergrund-Threads, AudioClips entstehen auf dem Hauptthread; versionierter Disk-Cache unter persistentDataPath/audiocache.</item>
    /// <item>Musik: sieben Stems je Stück, synchron per PlayScheduled gestartet; Lautstärke je Stem folgt Wiederherstellung, Tageszeit, Sturm und Wind; Überblendung bei Planetenwechsel.</item>
    /// <item>Intro-Score und Abspann mit vorgemerktem Start, falls die Erzeugung noch läuft; IntroTime ist DSP-genau.</item>
    /// <item>Sprachkanal: Lautstärke „Sprache“ (VoiceGain) und weiches Absenken der Musik (DuckMusic), solange der Erzähler (<see cref="Narrator"/>) spricht.</item>
    /// <item>3D-Effekte aus einem Quellen-Pool, Dauerklänge (Werkzeuge, Motor), planetentypische Ambience (Wind, Sturm, Wasser, Nacht) und Reaktion auf Spielereignisse (GameApp.OnFx).</item>
    /// <item>Radio (AudioRadio.cs) und Stadtklänge, die mit der Wiederherstellung wachsen (AudioCity.cs).</item>
    /// </list>
    /// Öffentliche Aufrufe werfen nie Ausnahmen; fehlende Clips werden still übersprungen (und im Hintergrund nachgeladen).
    /// </summary>
    public partial class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        const int PoolSize = 24;
        const int CacheFormat = 1;
        const float MinDist = 3f, MaxDist = 60f;
        enum Cat { Sfx, Ui, Ambient }

        // ================================================================== Erzeugung (Hintergrund)
        class Job { public string Key; public int Prio; public bool Load; public long Order; }
        class Product { public string Key; public int Rate; public float Length; public string[] Names; public float[][] Data; public float[] Rms; }

        readonly object qLock = new object();
        readonly List<Job> queue = new List<Job>();
        readonly HashSet<string> running = new HashSet<string>();
        readonly HashSet<string> wantLoad = new HashSet<string>();
        readonly Queue<Product> products = new Queue<Product>();
        readonly Queue<string> workerLog = new Queue<string>();
        Thread[] workers;
        volatile bool quitting;
        long jobOrder;
        string cacheDir;

        // ================================================================== Geladene Klänge
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        class MusicClips { public string Id; public AudioClip[] Stems; public float[] Rms; public float Length; public float LastUsed; }
        readonly Dictionary<string, MusicClips> music = new Dictionary<string, MusicClips>();
        AudioClip introClip;

        // ================================================================== Musik-Wiedergabe
        class Slot
        {
            public MusicClips Set;
            public AudioSource[] Src;
            public float Fade, FadeTarget, FadeSpeed = 0.4f;
            public readonly float[] Vol = new float[7];
            public bool Started;
        }
        readonly Slot[] slots = new Slot[2];
        int cur;
        float stormBlend, nightBlend, windBlend;

        // Intro / Abspann
        AudioSource introSrc;
        bool introWanted, introScheduled;
        double introStartDsp;
        bool endingWanted;

        // DSP-Zeit (geglättet zwischen den Audiopuffern)
        double lastDsp = -1, lastDspReal, lastDspOut;

        // ================================================================== Effekte
        class Voice { public AudioSource Src; public float Base; public Cat Cat; public string Id; public float Started; }
        readonly Voice[] pool = new Voice[PoolSize];
        class LoopVoice { public AudioSource Src; public string ClipId; public float Base = 1f, Cur, Target; public Cat Cat; public float AutoOff = -1f; }
        readonly Dictionary<string, LoopVoice> loops = new Dictionary<string, LoopVoice>();
        readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();
        Transform root;
        bool focused = true;
        float focusGain = 1f;
        string ambiencePlanet;
        float nextGrown;

        // ================================================================== Lebenszyklus
        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            try
            {
                GameData.EnsureLoaded();
                root = new GameObject("Audio").transform;
                root.SetParent(transform, false);
                for (int s = 0; s < 2; s++)
                {
                    var sl = new Slot { Src = new AudioSource[Synth.StemNames.Length] };
                    for (int k = 0; k < sl.Src.Length; k++) sl.Src[k] = NewSource("Musik" + s + "_" + Synth.StemNames[k], false, 0);
                    slots[s] = sl;
                }
                introSrc = NewSource("Intro", false, 0);
                for (int i = 0; i < PoolSize; i++) pool[i] = new Voice { Src = NewSource("Effekt" + i, false, 64) };
                cacheDir = Path.Combine(Application.persistentDataPath, "audiocache", "v" + CacheVersion());
                StartWorkers();
                // Reihenfolge: kurze Effekte → Menümusik → Intro → Planeten (nur Disk-Cache) → Ambience (nur Disk-Cache)
                foreach (var id in Synth.SfxIds)
                    if (!Synth.IsAmbience(id)) Request("sfx:" + id, id.StartsWith("ui_", StringComparison.Ordinal) ? 0 : 1, true);
                Request("music:menu", 2, true);
                Request("intro", 3, false);
                int p = 5;
                foreach (var id in Synth.MusicIds) if (id != "menu") Request("music:" + id, p++, false);
                foreach (var id in Synth.SfxIds) if (Synth.IsAmbience(id)) Request("sfx:" + id, 12, false);
            }
            catch (Exception e) { Debug.LogWarning("[Audio] Start: " + e.Message); }
        }

        void Start()
        {
            try
            {
                if (GameApp.I != null)
                {
                    GameApp.I.OnFx += HandleFx;
                    GameApp.I.OnPlanetChanged += HandlePlanetChanged;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Audio] Ereignisse: " + e.Message); }
        }

        void OnDestroy()
        {
            quitting = true;
            lock (qLock) Monitor.PulseAll(qLock);
            try
            {
                if (GameApp.I != null) { GameApp.I.OnFx -= HandleFx; GameApp.I.OnPlanetChanged -= HandlePlanetChanged; }
            }
            catch (Exception) { }
            if (I == this) I = null;
        }

        void OnApplicationQuit() { quitting = true; lock (qLock) Monitor.PulseAll(qLock); }

        void OnApplicationFocus(bool hasFocus) { focused = hasFocus; }

        AudioSource NewSource(string name, bool spatial, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.priority = priority;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = MinDist;
            s.maxDistance = MaxDist;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            return s;
        }

        static uint CacheVersion() { return Hash.Fnv1a("RePlanetAudio|" + Synth.Version + "|" + Synth.MusicRate + "|" + Synth.SfxRate + "|" + CacheFormat); }

        // ================================================================== Warteschlange / Threads
        void StartWorkers()
        {
            int n = SystemInfo.processorCount >= 4 ? 2 : 1;
            workers = new Thread[n];
            for (int i = 0; i < n; i++)
            {
                var t = new Thread(Work) { IsBackground = true, Name = "RePlanet-Audio" + i, Priority = System.Threading.ThreadPriority.BelowNormal };
                workers[i] = t;
                t.Start();
            }
            var cleanup = new Thread(CleanupOldCaches) { IsBackground = true, Name = "RePlanet-AudioCache", Priority = System.Threading.ThreadPriority.Lowest };
            cleanup.Start();
        }

        /// <summary>Fordert einen Klang an. load = false: nur im Disk-Cache vorbereiten (kein Speicherverbrauch im Spiel).</summary>
        void Request(string key, int prio, bool load)
        {
            if (load && IsLoaded(key)) return;
            lock (qLock)
            {
                if (load) wantLoad.Add(key);
                foreach (var j in queue)
                    if (j.Key == key)
                    {
                        if (prio < j.Prio) j.Prio = prio;
                        if (load) j.Load = true;
                        return;
                    }
                if (running.Contains(key)) return;
                queue.Add(new Job { Key = key, Prio = prio, Load = load, Order = jobOrder++ });
                Monitor.Pulse(qLock);
            }
        }

        bool IsLoaded(string key)
        {
            if (key == "intro") return introClip != null;
            if (key.StartsWith("sfx:", StringComparison.Ordinal)) return sfx.ContainsKey(key.Substring(4));
            if (key.StartsWith("music:", StringComparison.Ordinal)) return music.ContainsKey(key.Substring(6));
            return false;
        }

        void Work()
        {
            while (!quitting)
            {
                Job job = null;
                lock (qLock)
                {
                    while (queue.Count == 0 && !quitting) Monitor.Wait(qLock, 1000);
                    if (quitting) return;
                    int best = -1;
                    for (int i = 0; i < queue.Count; i++)
                        if (best < 0 || queue[i].Prio < queue[best].Prio || (queue[i].Prio == queue[best].Prio && queue[i].Order < queue[best].Order)) best = i;
                    if (best >= 0) { job = queue[best]; queue.RemoveAt(best); running.Add(job.Key); }
                }
                if (job == null) continue;
                try
                {
                    var p = Produce(job.Key);
                    bool load;
                    lock (qLock) load = job.Load || wantLoad.Contains(job.Key);
                    if (p != null && load) lock (products) products.Enqueue(p);
                }
                catch (Exception e) { lock (workerLog) workerLog.Enqueue(job.Key + ": " + e.Message); }
                finally { lock (qLock) running.Remove(job.Key); }
            }
        }

        Product Produce(string key)
        {
            var p = ReadCache(key);
            if (p != null) return p;
            p = new Product { Key = key };
            if (key == "intro")
            {
                int rate; float len;
                var d = Synth.IntroScore(out rate, out len);
                p.Rate = rate; p.Length = len; p.Names = new[] { "intro" }; p.Data = new[] { d };
            }
            else if (key.StartsWith("music:", StringComparison.Ordinal))
            {
                var m = Synth.Music(key.Substring(6));
                p.Rate = m.Rate; p.Length = m.Length; p.Names = Synth.StemNames;
                p.Data = new float[Synth.StemNames.Length][];
                for (int i = 0; i < p.Data.Length; i++) p.Data[i] = m.Stems[Synth.StemNames[i]];
            }
            else
            {
                var d = Synth.Sfx(key.Substring(4));
                p.Rate = Synth.SfxRate; p.Length = d.Length / (float)Synth.SfxRate; p.Names = new[] { key.Substring(4) }; p.Data = new[] { d };
            }
            p.Rms = new float[p.Data.Length];
            for (int i = 0; i < p.Data.Length; i++) p.Rms[i] = RmsOf(p.Data[i]);
            WriteCache(p);
            return p;
        }

        static float RmsOf(float[] d)
        {
            double s = 0;
            for (int i = 0; i < d.Length; i++) s += d[i] * d[i];
            return (float)Math.Sqrt(s / Math.Max(1, d.Length));
        }

        string CachePath(string key) { return Path.Combine(cacheDir, key.Replace(':', '_') + ".rpa"); }

        /// <summary>Cache-Format: Kennung, Version, Rate, Länge, Anzahl Spuren, je Spur Name + PCM16.</summary>
        void WriteCache(Product p)
        {
            try
            {
                Directory.CreateDirectory(cacheDir);
                string path = CachePath(p.Key), tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(0x31415052); w.Write(CacheVersion()); w.Write(p.Rate); w.Write(p.Length); w.Write(p.Data.Length);
                    for (int i = 0; i < p.Data.Length; i++)
                    {
                        w.Write(p.Names[i]);
                        var d = p.Data[i];
                        w.Write(d.Length);
                        var bytes = new byte[d.Length * 2];
                        for (int k = 0; k < d.Length; k++)
                        {
                            float v = d[k]; if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                            short s = (short)Math.Round(v * 32767f);
                            bytes[2 * k] = (byte)(s & 0xFF); bytes[2 * k + 1] = (byte)((s >> 8) & 0xFF);
                        }
                        w.Write(bytes);
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { lock (workerLog) workerLog.Enqueue("Cache schreiben " + p.Key + ": " + e.Message); }
        }

        Product ReadCache(string key)
        {
            try
            {
                string path = CachePath(key);
                if (!File.Exists(path)) return null;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadInt32() != 0x31415052 || r.ReadUInt32() != CacheVersion()) return null;
                    var p = new Product { Key = key, Rate = r.ReadInt32(), Length = r.ReadSingle() };
                    int n = r.ReadInt32();
                    if (n <= 0 || n > 16) return null;
                    p.Names = new string[n]; p.Data = new float[n][]; p.Rms = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        p.Names[i] = r.ReadString();
                        int len = r.ReadInt32();
                        if (len < 0 || len > 60 * 1000 * 1000) return null;
                        var bytes = r.ReadBytes(len * 2);
                        if (bytes.Length != len * 2) return null;
                        var d = new float[len];
                        for (int k = 0; k < len; k++) d[k] = (short)(bytes[2 * k] | (bytes[2 * k + 1] << 8)) / 32767f;
                        p.Data[i] = d;
                        p.Rms[i] = RmsOf(d);
                    }
                    return p;
                }
            }
            catch (Exception) { return null; }
        }

        void CleanupOldCaches()
        {
            try
            {
                string parent = Path.GetDirectoryName(cacheDir);
                if (!Directory.Exists(parent)) return;
                string mine = Path.GetFileName(cacheDir);
                foreach (var d in Directory.GetDirectories(parent))
                    if (Path.GetFileName(d) != mine) { try { Directory.Delete(d, true); } catch (Exception) { } }
            }
            catch (Exception) { }
        }

        /// <summary>Fertige Erzeugnisse als AudioClips anlegen (Hauptthread), höchstens ein großer Clip-Satz pro Bild.</summary>
        void PumpProducts()
        {
            int budget = 400000;
            while (budget > 0)
            {
                Product p;
                lock (products) { if (products.Count == 0) break; p = products.Dequeue(); }
                lock (qLock) wantLoad.Remove(p.Key);
                try
                {
                    if (p.Key == "intro")
                    {
                        if (introClip == null) introClip = MakeClip("Intro", p.Data[0], p.Rate);
                        budget -= p.Data[0].Length;
                    }
                    else if (p.Key.StartsWith("music:", StringComparison.Ordinal))
                    {
                        string id = p.Key.Substring(6);
                        if (!music.ContainsKey(id))
                        {
                            var mc = new MusicClips { Id = id, Length = p.Length, Stems = new AudioClip[Synth.StemNames.Length], Rms = new float[Synth.StemNames.Length], LastUsed = Time.unscaledTime };
                            for (int i = 0; i < Synth.StemNames.Length; i++)
                            {
                                int k = Array.IndexOf(p.Names, Synth.StemNames[i]);
                                if (k < 0) continue;
                                mc.Stems[i] = MakeClip("Musik_" + id + "_" + Synth.StemNames[i], p.Data[k], p.Rate);
                                mc.Rms[i] = p.Rms[k];
                            }
                            music[id] = mc;
                            TrimMusic();
                        }
                        budget -= p.Data.Length * p.Data[0].Length;
                    }
                    else
                    {
                        string id = p.Key.Substring(4);
                        if (!sfx.ContainsKey(id)) sfx[id] = MakeClip(id, p.Data[0], p.Rate);
                        budget -= p.Data[0].Length;
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Audio] Clip " + p.Key + ": " + e.Message); }
            }
            lock (workerLog) while (workerLog.Count > 0) Debug.LogWarning("[Audio] " + workerLog.Dequeue());
        }

        static AudioClip MakeClip(string name, float[] data, int rate)
        {
            if (data == null || data.Length == 0) return null;
            var c = AudioClip.Create(name, data.Length, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Höchstens drei Musik-Sätze im Speicher; nicht spielende, am längsten unbenutzte werden freigegeben.</summary>
        void TrimMusic()
        {
            while (music.Count > 3)
            {
                MusicClips victim = null;
                foreach (var m in music.Values)
                {
                    if (InUse(m) || m.Id == "menu" || (endingWanted && m.Id == "ending")) continue;
                    if (victim == null || m.LastUsed < victim.LastUsed) victim = m;
                }
                if (victim == null) return;
                music.Remove(victim.Id);
                foreach (var c in victim.Stems) if (c != null) Destroy(c);
            }
        }

        bool InUse(MusicClips m) { foreach (var s in slots) if (s != null && s.Set == m) return true; return false; }

        AudioClip Sfx(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            AudioClip c;
            if (sfx.TryGetValue(id, out c)) return c;
            Request("sfx:" + id, Synth.IsAmbience(id) ? 2 : 1, true);
            return null;
        }

        // ================================================================== Öffentliche Schnittstelle
        /// <summary>Einmaliger Effekt; pos = null → 2D (Oberfläche).</summary>
        public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitch = 1f)
        {
            if (I == null) return;
            try { I.PlayInternal(id, pos, volume, pitch, id != null && id.StartsWith("ui_", StringComparison.Ordinal) ? Cat.Ui : Cat.Sfx); }
            catch (Exception e) { Debug.LogWarning("[Audio] Play " + id + ": " + e.Message); }
        }

        /// <summary>Dauerklang an/aus/aktualisieren (z. B. Sauger, Motor). key identifiziert die Quelle.</summary>
        public static void Loop(string key, string clipId, bool on, Vector3? pos = null, float volume = 1f, float pitch = 1f)
        {
            if (I == null || key == null) return;
            try { I.LoopInternal(key, clipId, on, pos, volume, pitch, Cat.Sfx); }
            catch (Exception e) { Debug.LogWarning("[Audio] Loop " + key + ": " + e.Message); }
        }

        public static void Ui(string id)
        {
            if (I == null) return;
            try { I.PlayInternal(id, null, 1f, 1f, Cat.Ui); }
            catch (Exception e) { Debug.LogWarning("[Audio] Ui " + id + ": " + e.Message); }
        }

        /// <summary>Startet den Intro-Score. Ist er noch nicht erzeugt, wird der Start vorgemerkt (Erzeugung hat dann Vorrang) und erfolgt automatisch.</summary>
        public static void PlayIntro()
        {
            if (I == null) return;
            try
            {
                I.introWanted = true;
                I.introScheduled = false;
                I.introSrc.Stop();
                if (I.introClip == null) I.Request("intro", -10, true);
                else I.TryStartIntro();
            }
            catch (Exception e) { Debug.LogWarning("[Audio] PlayIntro: " + e.Message); }
        }

        /// <summary>Stoppt den Intro-Score und verwirft einen vorgemerkten Start.</summary>
        public static void StopIntro()
        {
            if (I == null) return;
            try { I.introWanted = false; I.introScheduled = false; I.introSrc.Stop(); }
            catch (Exception e) { Debug.LogWarning("[Audio] StopIntro: " + e.Message); }
        }

        /// <summary>Sekunden seit Beginn des Intro-Scores (DSP-genau, zwischen Audiopuffern geglättet); −1, solange die Wiedergabe nicht tatsächlich läuft.</summary>
        public static double IntroTime
        {
            get
            {
                if (I == null || !I.introScheduled) return -1;
                try
                {
                    double now = I.DspNow();
                    if (now < I.introStartDsp) return -1;
                    return now - I.introStartDsp;
                }
                catch (Exception) { return -1; }
            }
        }

        /// <summary>Intro-Score ist erzeugt und als Clip bereit.</summary>
        public static bool IntroReady { get { return I != null && I.introClip != null; } }

        /// <summary>Abspannmusik starten (wird bei Bedarf vorgemerkt, bis sie erzeugt ist).</summary>
        public static void PlayEnding()
        {
            if (I == null) return;
            try { I.endingWanted = true; if (!I.music.ContainsKey("ending")) I.Request("music:ending", -10, true); }
            catch (Exception e) { Debug.LogWarning("[Audio] PlayEnding: " + e.Message); }
        }

        /// <summary>Abspannmusik beenden (verwirft auch einen vorgemerkten Start); danach übernimmt wieder die Planetenmusik.</summary>
        public static void StopEnding()
        {
            if (I == null) return;
            try { I.endingWanted = false; }
            catch (Exception) { }
        }

        // ================================================================== Effekte
        float CatVolume(Cat c)
        {
            var s = GameApp.I != null ? GameApp.I.Settings : null;
            if (s == null) return 1f;
            switch (c)
            {
                case Cat.Ui: return s.UiVolume;
                case Cat.Ambient: return s.AmbientVolume;
                default: return s.SfxVolume;
            }
        }

        void PlayInternal(string id, Vector3? pos, float volume, float pitch, Cat cat)
        {
            var clip = Sfx(id);
            if (clip == null || volume <= 0f) return;
            float now = Time.unscaledTime;
            // Doppelte Auslösung im selben Moment (z. B. Effekt- und Klangsystem reagieren auf dasselbe Ereignis) zusammenfassen
            string dk = id + (pos.HasValue ? "@" + Mathf.RoundToInt(pos.Value.x) + "," + Mathf.RoundToInt(pos.Value.z) : "");
            float last;
            if (lastPlay.TryGetValue(dk, out last) && now - last < 0.05f) return;
            lastPlay[dk] = now;
            if (lastPlay.Count > 256) lastPlay.Clear();
            int same = 0;
            Voice free = null, steal = null;
            float stealScore = float.MaxValue;
            foreach (var v in pool)
            {
                bool busy = v.Src.isPlaying;
                if (busy && v.Id == id) same++;
                if (!busy) { if (free == null) free = v; continue; }
                float remain = v.Src.clip != null ? 1f - Mathf.Clamp01(v.Src.time / Mathf.Max(0.01f, v.Src.clip.length)) : 0f;
                float score = v.Base * remain;
                if (score < stealScore) { stealScore = score; steal = v; }
            }
            if (same >= 4) return;
            var voice = free ?? steal;
            if (voice == null) return;
            var src = voice.Src;
            src.Stop();
            src.clip = clip;
            src.loop = false;
            src.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            if (pos.HasValue) { src.spatialBlend = 1f; src.transform.position = pos.Value; }
            else { src.spatialBlend = 0f; src.transform.localPosition = Vector3.zero; }
            src.panStereo = 0f;
            voice.Base = Mathf.Clamp01(volume);
            voice.Cat = cat;
            voice.Id = id;
            voice.Started = now;
            src.volume = voice.Base * CatVolume(cat) * focusGain;
            src.Play();
        }

        void LoopInternal(string key, string clipId, bool on, Vector3? pos, float volume, float pitch, Cat cat)
        {
            LoopVoice lv;
            if (!loops.TryGetValue(key, out lv))
            {
                if (!on) return;
                lv = new LoopVoice { Src = NewSource("Dauer_" + key, pos.HasValue, 96), Cat = cat };
                lv.Src.loop = true;
                loops[key] = lv;
            }
            lv.Target = on ? 1f : 0f;
            if (!on) return;
            lv.Base = Mathf.Clamp01(volume);
            lv.Cat = cat;
            lv.Src.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            if (pos.HasValue) { lv.Src.spatialBlend = 1f; lv.Src.transform.position = pos.Value; }
            else lv.Src.spatialBlend = 0f;
            if (lv.ClipId != clipId)
            {
                var c = Sfx(clipId);
                if (c == null) return; // wird nachgeladen; beim nächsten Aufruf erneut versucht
                lv.ClipId = clipId;
                bool was = lv.Src.isPlaying;
                lv.Src.clip = c;
                lv.Src.time = lv.Src.clip.length > 0.5f ? UnityEngine.Random.Range(0f, lv.Src.clip.length * 0.9f) : 0f;
                if (was) lv.Src.Play();
            }
        }

        void UpdateVoices(float dt)
        {
            foreach (var v in pool) if (v.Src.isPlaying) v.Src.volume = v.Base * CatVolume(v.Cat) * focusGain;
            foreach (var kv in loops)
            {
                var lv = kv.Value;
                if (lv.AutoOff > 0f) { lv.AutoOff -= dt; if (lv.AutoOff <= 0f) lv.Target = 0f; }
                lv.Cur = Mathf.MoveTowards(lv.Cur, lv.Target, dt * (lv.Cat == Cat.Ambient ? 0.5f : 8f));
                if (lv.Src.clip == null) continue;
                lv.Src.volume = lv.Cur * lv.Base * CatVolume(lv.Cat) * focusGain;
                if (lv.Cur > 0.001f && !lv.Src.isPlaying) lv.Src.Play();
                else if (lv.Cur <= 0.001f && lv.Target <= 0f && lv.Src.isPlaying) lv.Src.Stop();
            }
        }

        // ================================================================== Hauptschleife
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            try
            {
                PumpProducts();
                var app = GameApp.I;
                if (app != null && app.Settings != null && !Mathf.Approximately(AudioListener.volume, app.Settings.MasterVolume)) AudioListener.volume = app.Settings.MasterVolume;
                bool mute = app != null && app.Settings != null && app.Settings.MuteWhenUnfocused && !focused;
                focusGain = Mathf.MoveTowards(focusGain, mute ? 0f : 1f, dt * 3f);
                UpdateDuck(dt);
                UpdateIntro();
                UpdateRadio(dt);
                UpdateMusic(dt);
                UpdateAmbience(dt);
                UpdateVoices(dt);
            }
            catch (Exception e) { Debug.LogWarning("[Audio] Update: " + e.Message); }
        }

        double DspNow()
        {
            double dsp = AudioSettings.dspTime, real = Time.realtimeSinceStartupAsDouble;
            if (dsp != lastDsp) { lastDsp = dsp; lastDspReal = real; }
            double est = lastDsp + Math.Min(real - lastDspReal, 0.1);
            if (est < lastDspOut && lastDspOut - est < 0.1) est = lastDspOut; // monoton
            lastDspOut = est;
            return est;
        }

        void TryStartIntro()
        {
            if (!introWanted || introScheduled || introClip == null) return;
            introSrc.clip = introClip;
            introSrc.loop = false;
            introSrc.volume = MusicVolume() * focusGain;
            introStartDsp = AudioSettings.dspTime + 0.2;
            introSrc.PlayScheduled(introStartDsp);
            introScheduled = true;
        }

        void UpdateIntro()
        {
            if (introWanted && !introScheduled)
            {
                if (introClip != null) TryStartIntro();
                else Request("intro", -10, true);
            }
            if (introScheduled)
            {
                introSrc.volume = MusicVolume() * focusGain;
                if (IntroTime > introClip.length + 0.5f) { introScheduled = false; introWanted = false; }
            }
        }

        /// <summary>Musiklautstärke aus den Einstellungen, abgesenkt, solange der Erzähler spricht (Ducking).</summary>
        float MusicVolume() { var app = GameApp.I; return (app != null && app.Settings != null ? app.Settings.MusicVolume : 0.7f) * (1f - duck); }

        // ================================================================== Sprachkanal (Erzähler)
        float duck, duckReq, duckReqAt = -10f;

        /// <summary>
        /// Senkt die Musik (Intro-Score, Abspann, Planetenmusik) weich ab, solange der Aufruf jedes Bild wiederholt wird.
        /// amount 0 … 0,9 = Anteil der Absenkung. Ohne weitere Aufrufe kehrt die Musik nach kurzer Zeit weich zurück.
        /// </summary>
        public static void DuckMusic(float amount)
        {
            if (I == null) return;
            I.duckReq = Mathf.Clamp(amount, 0f, 0.9f);
            I.duckReqAt = Time.unscaledTime;
        }

        /// <summary>Lautstärke des Sprachkanals (Einstellung „Sprache“ × Fokus-Stummschaltung).</summary>
        public static float VoiceGain
        {
            get
            {
                if (I == null) return 0.8f;
                var app = GameApp.I;
                return (app != null && app.Settings != null ? app.Settings.VoiceVolume : 0.8f) * I.focusGain;
            }
        }

        /// <summary>Elternobjekt für Sprachquellen (unter dem Audio-Knoten); null, solange kein AudioManager existiert.</summary>
        public static Transform VoiceParent { get { return I != null ? I.root : null; } }

        void UpdateDuck(float dt)
        {
            float target = Time.unscaledTime - duckReqAt < 0.2f ? duckReq : 0f;
            // schnell absenken (≈ 0,3 s), langsam zurück (≈ 1,2 s) – kein Pumpen zwischen zwei Sätzen
            duck = Mathf.MoveTowards(duck, target, dt * (target > duck ? 2.0f : 0.5f));
        }

        /// <summary>Welche Musik gerade laufen soll (null = keine).</summary>
        string WantedMusic()
        {
            var app = GameApp.I;
            if (endingWanted) return "ending";
            if (app == null) return "menu";
            switch (app.Mode)
            {
                case AppMode.Intro: return null;
                case AppMode.Menu:
                case AppMode.PlanetSelect: return "menu";
                case AppMode.Loading: return slots[cur].Set != null ? slots[cur].Set.Id : "menu";
                default:
                    if (app.InGame)
                    {
                        string radio;
                        if (RadioOverride(out radio)) return radio;
                        return app.W.CurrentPlanet;
                    }
                    return slots[cur].Set != null ? slots[cur].Set.Id : "menu";
            }
        }

        void UpdateMusic(float dt)
        {
            string want = WantedMusic();
            bool introPlaying = introScheduled || introWanted;
            if (introPlaying) want = null;
            var active = slots[cur];
            string activeId = active.Set != null && active.FadeTarget > 0f ? active.Set.Id : null;
            if (want != activeId)
            {
                if (want == null) active.FadeTarget = 0f;
                else
                {
                    MusicClips mc;
                    if (music.TryGetValue(want, out mc))
                    {
                        // Überblendung in den anderen Slot, alle Stems sample-genau gleichzeitig starten
                        int other = 1 - cur;
                        var s = slots[other];
                        StopSlot(s);
                        s.Set = mc;
                        double at = AudioSettings.dspTime + 0.12;
                        for (int k = 0; k < s.Src.Length; k++)
                        {
                            s.Src[k].clip = mc.Stems[k];
                            s.Src[k].loop = true;
                            s.Src[k].volume = 0f;
                            if (mc.Stems[k] != null) s.Src[k].PlayScheduled(at);
                        }
                        s.Started = true;
                        s.Fade = 0f; s.FadeTarget = 1f;
                        s.FadeSpeed = want == "ending" ? 0.7f : active.Set == null ? 0.6f : 0.4f;
                        active.FadeTarget = 0f;
                        active.FadeSpeed = want == "ending" ? 0.7f : 0.4f;
                        ComputeTargets(s, mc, true);
                        cur = other;
                        active = s;
                    }
                    else Request("music:" + want, want == "ending" ? -10 : -5, true);
                }
            }
            // Planetenwechsel vorausschauend laden
            var app = GameApp.I;
            if (app != null && app.InGame && !music.ContainsKey(app.W.CurrentPlanet)) Request("music:" + app.W.CurrentPlanet, -5, true);

            float mv = MusicVolume() * focusGain;
            foreach (var s in slots)
            {
                if (!s.Started) continue;
                s.Fade = Mathf.MoveTowards(s.Fade, s.FadeTarget, dt * s.FadeSpeed);
                if (s.Set == null) continue;
                s.Set.LastUsed = Time.unscaledTime;
                ComputeTargets(s, s.Set, false, dt);
                for (int k = 0; k < s.Src.Length; k++) s.Src[k].volume = Mathf.Clamp01(s.Vol[k]) * s.Fade * mv;
                if (s.Fade <= 0f && s.FadeTarget <= 0f) StopSlot(s);
            }
        }

        void StopSlot(Slot s)
        {
            foreach (var src in s.Src) { src.Stop(); src.clip = null; src.volume = 0f; }
            s.Set = null; s.Started = false; s.Fade = 0f; s.FadeTarget = 0f;
        }

        static float Ramp(float x, float th) { return Mathf.SmoothStep(0f, 1f, (x - th + 0.05f) / 0.1f); }

        /// <summary>Lautstärke je Stem aus dem Spielkontext (Wiederherstellung, Nacht, Sturm, Wind) inkl. Lautheitsausgleich bei wenigen Schichten.</summary>
        void ComputeTargets(Slot s, MusicClips mc, bool snap, float dt = 0f)
        {
            // Reihenfolge wie Synth.StemNames: pad, piano, bass, choir, brass, perc, wind
            var t = new float[7];
            for (int k = 0; k < 7; k++) t[k] = 1f;
            var app = GameApp.I;
            var radioMix = RadioMixFor(mc);
            bool planetMusic = radioMix == null && app != null && app.InGame && mc.Id == app.W.CurrentPlanet;
            if (planetMusic)
            {
                var w = app.W; var ps = w.Cur; string pl = w.CurrentPlanet;
                float rest = Rules.PlanetRestoration(w, ps);
                float dark = Rules.Darkness(Rules.DayPhase(w, pl));
                float dx, dz;
                float wind = Rules.Wind(w, pl, out dx, out dz);
                float stormT = ps.StormActive ? 1f : ps.StormWarn ? 0.5f : 0f;
                float k = snap ? 1f : 1f - Mathf.Exp(-dt / 3f);
                stormBlend += (stormT - stormBlend) * k;
                nightBlend += (dark - nightBlend) * k;
                windBlend += (wind - windBlend) * (snap ? 1f : 1f - Mathf.Exp(-dt / 1.5f));
                t[0] = 1f;
                t[1] = Ramp(rest, 0.1f);
                t[2] = Ramp(rest, 0.2f);
                t[3] = Ramp(rest, 0.4f);
                t[4] = Ramp(rest, 0.6f);
                t[5] = 0.3f + 0.7f * Ramp(rest, 0.3f);
                t[6] = 0.35f + 0.9f * windBlend;
                // Nacht: melodische Schichten leiser, Fläche/Wind/Klavier sparsam
                float n = nightBlend;
                t[0] *= 1f - 0.35f * n; t[1] *= 1f - 0.45f * n; t[2] *= 1f - 0.4f * n; t[3] *= 1f - 0.6f * n; t[4] *= 1f - 0.65f * n; t[5] *= 1f - 0.5f * n;
                t[6] *= 1f - 0.2f * n;
                // Sturm: Schlagwerk, Blech und Wind hoch – Spannung; zarte Schichten zurück
                float sb = stormBlend;
                t[5] = Mathf.Lerp(t[5], 1f, sb);
                t[4] = Mathf.Lerp(t[4], 0.9f, sb);
                t[6] = Mathf.Lerp(t[6], 1.3f, sb);
                t[1] *= 1f - 0.5f * sb; t[3] *= 1f - 0.3f * sb;
                // Lautheitsausgleich: wenige Schichten → bis +4,6 dB
                double full = 0, now = 0;
                for (int i = 0; i < 7; i++) { float r = mc.Rms != null && i < mc.Rms.Length ? mc.Rms[i] : 0.05f; full += r * r; now += (t[i] * r) * (t[i] * r); }
                float makeup = now > 1e-9 ? Mathf.Clamp(Mathf.Sqrt((float)(0.45 * full / now)), 1f, 1.7f) : 1f;
                for (int i = 0; i < 7; i++) t[i] *= makeup * 0.85f;
            }
            else if (radioMix != null)
            {
                // Radio: feste Mischung des Stücks, gleicher Lautheitsausgleich wie bei der Planetenmusik
                double full = 0, now = 0;
                for (int i = 0; i < 7; i++)
                {
                    t[i] = i < radioMix.Length ? radioMix[i] : 0f;
                    float r = mc.Rms != null && i < mc.Rms.Length ? mc.Rms[i] : 0.05f;
                    full += r * r; now += (t[i] * r) * (t[i] * r);
                }
                float makeup = now > 1e-9 ? Mathf.Clamp(Mathf.Sqrt((float)(0.45 * full / now)), 1f, 1.7f) : 1f;
                for (int i = 0; i < 7; i++) t[i] *= makeup * 0.85f;
            }
            else if (mc.Id == "menu") { t[6] = 0.8f; }
            float a = snap ? 1f : 1f - Mathf.Exp(-dt / 1.6f);
            for (int i = 0; i < 7; i++) s.Vol[i] += (t[i] - s.Vol[i]) * a;
        }

        // ================================================================== Ambience
        void UpdateAmbience(float dt)
        {
            var app = GameApp.I;
            bool inGame = app != null && app.InGame && app.Mode == AppMode.Playing && !introScheduled && !endingWanted;
            if (!inGame)
            {
                AmbLoop("amb_wind", null, false, 0f, 0f);
                AmbLoop("amb_storm", null, false, 0f, 0f);
                AmbLoop("amb_water", null, false, 0f, 0f);
                AmbLoop("amb_night", null, false, 0f, 0f);
                CityOff();
                return;
            }
            var w = app.W; var ps = w.Cur; string pl = w.CurrentPlanet;
            if (ambiencePlanet != pl) { ambiencePlanet = pl; PrefetchAmbience(pl); }
            PlanetDef def;
            GameData.Planets.TryGetValue(pl, out def);
            float dx, dz;
            float wind = Rules.Wind(w, pl, out dx, out dz);
            float storm = ps.StormActive ? 1f : ps.StormWarn ? 0.35f : 0f;
            float dark = Rules.Darkness(Rules.DayPhase(w, pl));
            var me = app.Me;
            float shelter = me != null && me.ShelterKind > 0 ? 0.45f : 1f;
            // Windrichtung relativ zur Kamera → leichte Stereo-Verschiebung
            float pan = 0f;
            Camera cam = CameraRig.I != null ? CameraRig.I.Cam : null;
            if (cam != null)
            {
                var right = cam.transform.right;
                pan = Mathf.Clamp((right.x * dx + right.z * dz) * 0.45f, -0.45f, 0.45f);
            }
            AmbLoop("amb_wind", "wind_" + pl, true, (0.2f + 0.8f * wind) * (1f - 0.6f * storm) * shelter, pan);
            AmbLoop("amb_storm", "storm_" + pl, storm > 0.01f, storm * 0.9f * shelter, pan * 0.6f);
            float water = 0f;
            if (def != null && def.Water && cam != null)
            {
                float above = cam.transform.position.y - Core.Terrain.WaterLevel(pl);
                water = above < 0f ? 0.25f : 1f - Mathf.Clamp01(above / 30f) * 0.75f;
            }
            AmbLoop("amb_water", "water_loop", water > 0.01f, water * 0.7f, 0f);
            AmbLoop("amb_night", "night_ambience", dark > 0.05f, dark * (1f - storm) * 0.8f, 0f);
            UpdateCity(dt, w, ps, pl, dark, storm, wind, shelter);
        }

        void AmbLoop(string key, string clip, bool on, float vol, float pan)
        {
            LoopInternal(key, clip, on && vol > 0.001f, null, Mathf.Clamp01(vol), 1f, Cat.Ambient);
            LoopVoice lv;
            if (loops.TryGetValue(key, out lv)) lv.Src.panStereo = pan;
        }

        void PrefetchAmbience(string pl)
        {
            Request("sfx:wind_" + pl, 2, true);
            Request("sfx:storm_" + pl, 3, true);
            Request("sfx:night_ambience", 4, true);
            PlanetDef def;
            if (GameData.Planets.TryGetValue(pl, out def) && def.Water) Request("sfx:water_loop", 3, true);
        }

        void HandlePlanetChanged(string planet)
        {
            try
            {
                if (string.IsNullOrEmpty(planet)) return;
                Request("music:" + planet, -5, true);
                PrefetchAmbience(planet);
            }
            catch (Exception e) { Debug.LogWarning("[Audio] Planetenwechsel: " + e.Message); }
        }

        // ================================================================== Spielereignisse
        Vector3? FxPos(JObj f)
        {
            var p = f.Floats("pos");
            if (p != null && p.Length >= 3) return new Vector3(p[0], p[1], p[2]);
            var app = GameApp.I;
            if (app == null || app.W == null) return null;
            string pid = f.Str("pid");
            PlayerData pd;
            if (pid != null && app.W.Players.TryGetValue(pid, out pd)) return new Vector3(pd.Pos.x, pd.Pos.y, pd.Pos.z);
            var me = app.Me;
            return me != null ? new Vector3(me.Pos.x, me.Pos.y, me.Pos.z) : (Vector3?)null;
        }

        bool Mine(JObj f)
        {
            var app = GameApp.I;
            return app != null && app.Client != null && f.Str("pid") == app.Client.Pid;
        }

        static string MaterialSound(string trashType)
        {
            TrashType tt;
            if (trashType == null || !GameData.Trash.TryGetValue(trashType, out tt)) return "grab";
            switch (tt.MainMaterial)
            {
                case "papier": return "paper";
                case "glas": return "glass";
                case "kunststoff":
                case "netz": return "plastic";
                case "metall":
                case "stahl":
                case "kupfer":
                case "seltenmetall": return "metal";
                default: return "grab";
            }
        }

        void HandleFx(JObj f)
        {
            if (f == null) return;
            try
            {
                string k = f.Str("k");
                if (k == null) return;
                var pos = FxPos(f);
                switch (k)
                {
                    case "collect":
                        {
                            string tool = f.Str("tool");
                            float v = tool == "vacuum" ? 0.5f : tool == "rover" || tool == "boat" ? 0.6f : 0.85f;
                            PlayInternal(MaterialSound(f.Str("t")), pos, v, UnityEngine.Random.Range(0.92f, 1.08f), Cat.Sfx);
                            TrashType tt;
                            if (f.Str("t") != null && GameData.Trash.TryGetValue(f.Str("t"), out tt) && tt.Floating) PlayInternal("splash", pos, 0.35f, 1.1f, Cat.Sfx);
                            break;
                        }
                    case "magnetwave": PlayInternal("magnet_wave", pos, 0.9f, 1f, Cat.Sfx); break;
                    case "cut": PlayInternal("metal", pos, 0.8f, 0.8f, Cat.Sfx); PlayInternal("unload", pos, 0.5f, 1f, Cat.Sfx); break;
                    case "thawed": PlayInternal("steam", pos, 0.8f, 1f, Cat.Sfx); PlayInternal("ice_crack", pos, 0.6f, 1f, Cat.Sfx); break;
                    case "oilclean": PlayInternal("bubbles", pos, 0.8f, 0.9f, Cat.Sfx); PlayInternal("splash", pos, 0.4f, 0.8f, Cat.Sfx); break;
                    case "press": PlayInternal("press", pos, 0.9f, 1f, Cat.Sfx); break;
                    case "bale": PlayInternal("bale", pos, 0.9f, 1f, Cat.Sfx); break;
                    case "deposit":
                    case "vload":
                    case "vunload":
                    case "wreckdone":
                    case "sort": PlayInternal("unload", pos, 0.8f, 1f, Cat.Sfx); break;
                    case "sell":
                    case "dispose":
                    case "buymat": if (Mine(f) || k == "buymat") PlayInternal("coin", null, 0.7f, 1f, Cat.Ui); break;
                    case "contract": PlayInternal("coin", null, 0.7f, 1f, Cat.Ui); break;
                    case "upgrade":
                    case "vehicle":
                    case "ship":
                    case "build":
                    case "projectstart":
                    case "sheltered":
                        PlayInternal("build", pos, 0.9f, 1f, Cat.Sfx);
                        PlayInternal("beep_happy", pos, 0.6f, 1f, Cat.Sfx);
                        break;
                    case "repaired": PlayInternal("repair", pos, 0.9f, 1f, Cat.Sfx); break;
                    case "plant": PlayInternal("plant", pos, 0.9f, 1f, Cat.Sfx); break;
                    case "grown":
                        if (Time.unscaledTime >= nextGrown) { nextGrown = Time.unscaledTime + 2.5f; PlayInternal("plant", null, 0.3f, 1.1f, Cat.Sfx); }
                        break;
                    case "lore": PlayInternal("lore", null, 0.9f, 1f, Cat.Ui); break;
                    case "view": PlayInternal("mission_new", null, 0.5f, 1f, Cat.Ui); break;
                    case "zone": PlayInternal("zone", null, 0.9f, 1f, Cat.Ui); break;
                    case "gate": PlayInternal("gate_open", null, 0.9f, 1f, Cat.Sfx); break;
                    case "areaclean":
                    case "mission_done":
                    case "unlock":
                    case "eco": PlayInternal("mission", null, 0.9f, 1f, Cat.Ui); break;
                    case "mission_new": PlayInternal("mission_new", null, 0.8f, 1f, Cat.Ui); break;
                    case "cosmetic": PlayInternal("beep_happy", null, 0.6f, 1.1f, Cat.Ui); break;
                    case "awaken": PlayInternal("awaken", null, 1f, 1f, Cat.Sfx); break;
                    case "stormwarn":
                        PlayInternal("thunder", null, 0.35f, 0.8f, Cat.Ambient);
                        PlayInternal("whoosh", null, 0.4f, 0.8f, Cat.Ambient);
                        break;
                    case "storm":
                        if (f.Bool("on")) { PlayInternal("thunder", null, 0.9f, 1f, Cat.Ambient); PlayInternal("whoosh", null, 0.7f, 0.7f, Cat.Ambient); }
                        else PlayInternal("whoosh", null, 0.35f, 1.1f, Cat.Ambient);
                        break;
                    case "nightfall":
                        PlayInternal("whoosh", null, 0.3f, 0.6f, Cat.Ambient);
                        PlayInternal("beep_curious", FxPos(new JObj()), 0.5f, 0.85f, Cat.Sfx);
                        break;
                    case "daybreak": PlayInternal("beep_happy", FxPos(new JObj()), 0.4f, 1f, Cat.Sfx); break;
                    case "morning": PlayInternal("beep_happy", FxPos(new JObj()), 0.7f, 1f, Cat.Sfx); break;
                    case "shutdown": PlayInternal("beep_sad", pos, 1f, 0.9f, Cat.Sfx); break;
                    case "towed":
                        {
                            LoopInternal("tow", "drone_loop", true, pos, 0.6f, 1f, Cat.Sfx);
                            LoopVoice lv;
                            if (loops.TryGetValue("tow", out lv)) lv.AutoOff = 3f;
                            break;
                        }
                    case "sleep": PlayInternal("beep_ok", pos, 0.6f, 0.8f, Cat.Sfx); break;
                    case "lifted":
                    case "onrover":
                    case "dropped":
                    case "movebuild": PlayInternal("crane", pos, 0.8f, 1f, Cat.Sfx); break;
                    case "demolish": PlayInternal("unload", pos, 0.8f, 0.8f, Cat.Sfx); PlayInternal("metal", pos, 0.6f, 0.7f, Cat.Sfx); break;
                    case "delivery": PlayInternal("whoosh", null, 0.4f, 1.2f, Cat.Sfx); PlayInternal("unload", null, 0.4f, 1f, Cat.Sfx); break;
                    case "dronepick": PlayInternal("grab", pos, 0.4f, 1.2f, Cat.Sfx); break;
                    case "travel":
                    case "vreset": PlayInternal("whoosh", null, 0.8f, 1f, Cat.Sfx); break;
                    case "arrive": PlayInternal("whoosh", null, 0.5f, 0.8f, Cat.Sfx); break;
                    case "venter": PlayInternal("beep_ok", pos, 0.6f, 1f, Cat.Sfx); break;
                    case "join":
                    case "leave":
                    case "help": if (!Mine(f)) PlayInternal("beep_curious", null, 0.6f, 1f, Cat.Ui); break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Audio] Ereignis: " + e.Message); }
        }
    }
}
