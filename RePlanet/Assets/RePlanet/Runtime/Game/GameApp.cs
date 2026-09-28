using System;
using System.Collections.Generic;
using System.IO;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    public enum AppMode { Menu, Intro, PlanetSelect, Loading, Playing, Ending }

    /// <summary>
    /// Zentrale Steuerung: startet Solo- und Koop-Sitzungen, verbindet Gäste, speichert, leitet Ereignisse weiter.
    /// Wird automatisch beim Spielstart erzeugt – es ist keine Szene mit vorbereiteten Objekten nötig.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        [NonSerialized] public Settings Settings;
        [NonSerialized] public Profile Profile;
        [NonSerialized] public SaveStore Saves;
        [NonSerialized] public HostServer Host;
        [NonSerialized] public GameClient Client;
        public string Slot = "auto";
        public AppMode Mode = AppMode.Menu;
        public string LoadingText = "";
        public string PendingWorldName = "Meine Welt";
        public string PendingSlot = "auto";
        public string LastError;
        float loadingTimeout;
        string joinCode;

        public event Action<JObj> OnFx;
        public event Action<string> OnPlanetChanged;
        public event Action OnSessionStarted, OnSessionEnded;
        /// <summary>Intro abspielen; Parameter = Rückruf nach Ende/Überspringen.</summary>
        public event Action<Action> OnIntroRequested;
        /// <summary>Abspann abspielen; Parameter = Rückruf nach Ende.</summary>
        public event Action<Action> OnEndingRequested;

        public WorldState W { get { return Client != null && Client.Joined ? Client.W : null; } }
        public PlayerData Me { get { return Client != null ? Client.Me : null; } }
        public bool InGame { get { return Mode == AppMode.Playing && W != null; } }
        public bool IsHost { get { return Host != null; } }
        public bool IsGuest { get { return Client != null && Host == null; } }
        public bool CoopOpen { get { return Host != null && Host.Online; } }
        public bool CoopActive { get { return IsGuest || (Host != null && (Host.Online || Host.Session.OnlineCount > 1)); } }
        /// <summary>Im Solo-Spiel steht die Welt still, solange ein Menü offen ist.</summary>
        public bool Paused { get { return InGame && !CoopActive && (UIState.BlocksGameplay || Mode == AppMode.Ending); } }
        public string SaveDir { get { return Path.Combine(Application.persistentDataPath, "saves"); } }
        public string PhotoDir { get { return Path.Combine(Application.persistentDataPath, "Fotos"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("RE:PLANET");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        /// <summary>Weitere Komponenten melden sich hier an, damit sie beim Start mit erzeugt werden.</summary>
        public static readonly List<Type> Components = new List<Type>();

        static void Register(string typeName)
        {
            var t = Type.GetType("RePlanet." + typeName);
            if (t != null && !Components.Contains(t)) Components.Add(t);
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.runInBackground = true;
            GameData.EnsureLoaded();
            Settings = Settings.Load();
            Settings.Apply();
            Profile = Profile.Load();
            Saves = new SaveStore(SaveDir);
            // Bekannte Bausteine (existieren sie nicht, wird nichts erzeugt)
            foreach (var n in new[] { "AudioManager", "WorldView", "CameraRig", "PlayerController", "IntroDirector", "EndingDirector", "UIRoot" }) Register(n);
            foreach (var t in Components) if (GetComponent(t) == null) gameObject.AddComponent(t);
            Mode = AppMode.Menu;
            UIState.Open(UIScreen.MainMenu);
        }

        // ================================================================== Abläufe
        /// <summary>
        /// Neues Spiel: Intro → Planetenwahl → Welt erzeugen. Das Intro gehört zu jedem neuen Spiel und läuft immer
        /// (überspringbar durch Gedrückthalten); <paramref name="playIntro"/> wird nur noch aus Kompatibilität angenommen.
        /// </summary>
        public void BeginNewGame(string worldName, string slot, bool playIntro = true)
        {
            PendingWorldName = string.IsNullOrEmpty(worldName) ? "Meine Welt" : worldName.Trim();
            PendingSlot = string.IsNullOrEmpty(slot) ? "auto" : slot;
            if (OnIntroRequested != null)
            {
                Mode = AppMode.Intro;
                UIState.Open(UIScreen.Intro);
                OnIntroRequested(IntroFinished);
            }
            else IntroFinished();
        }

        /// <summary>Nur für interne Zwecke (Tests, Entwicklung): Intro ohne Spielstart, danach zurück ins Hauptmenü.</summary>
        public void PlayIntroOnly()
        {
            if (OnIntroRequested == null) return;
            Mode = AppMode.Intro;
            UIState.Open(UIScreen.Intro);
            OnIntroRequested(() => { Mode = AppMode.Menu; UIState.Open(UIScreen.MainMenu); });
        }

        void IntroFinished()
        {
            MarkIntroSeen();
            Mode = AppMode.PlanetSelect;
            UIState.Open(UIScreen.PlanetSelect);
        }

        void MarkIntroSeen()
        {
            if (Settings.IntroSeenOnce) return;
            Settings.IntroSeenOnce = true;
            Settings.Save();
        }

        /// <summary>
        /// Gast im Koop, der das Intro auf diesem Profil noch nie gesehen hat: nach dem Beitritt zuerst das Intro.
        /// Die Welt läuft beim Host weiter; der Gast steht so lange am Stützpunkt (Unterschlupf, Ladestation) – Nacht
        /// und Sturm führen dort nicht zur Notabschaltung. Der Weltaufbau läuft bereits im Hintergrund.
        /// </summary>
        void PlayGuestIntro()
        {
            Mode = AppMode.Intro;
            UIState.Open(UIScreen.Intro);
            OnIntroRequested(() =>
            {
                MarkIntroSeen();
                // Sitzung inzwischen beendet (Host weg, Verbindung verloren)? Dann steht die Meldung schon im Menü.
                if (Client == null || !Client.Joined || W == null)
                {
                    if (Mode == AppMode.Intro) { Mode = AppMode.Menu; UIState.Open(UIScreen.MainMenu); }
                    return;
                }
                Mode = AppMode.Playing;
                UIState.Open(UIScreen.None);
                // Planetenwechsel während des Intros wurde von der Darstellung nicht übernommen (nur im Spiel/Laden) → nachholen
                if (WorldView.I != null && WorldView.I.Planet != W.CurrentPlanet)
                {
                    try { OnPlanetChanged?.Invoke(W.CurrentPlanet); } catch (Exception e) { Debug.LogException(e); }
                }
            });
        }

        /// <summary>Von der Planetenwahl aufgerufen: erzeugt die Welt und startet sie.</summary>
        public void StartNewWorld(string startPlanet)
        {
            var w = Game.NewWorld(PendingWorldName, startPlanet);
            w.IntroSeen = true;
            string err;
            if (!Saves.Save(PendingSlot, w, out err)) Hud.Show(err, ToastKind.Error, 6f);
            StartLocal(w, PendingSlot);
        }

        public bool Continue(string slot)
        {
            string err; bool fromBackup;
            var w = Saves.Load(slot, out err, out fromBackup);
            if (w == null)
            {
                UIState.Message("Spielstand nicht ladbar", err ?? "Unbekannter Fehler.", UIScreen.Saves);
                return false;
            }
            if (fromBackup) Hud.Show(err, ToastKind.Warning, 8f);
            StartLocal(w, slot);
            return true;
        }

        public void StartLocal(WorldState w, string slot)
        {
            EndSession(false);
            Slot = slot;
            Host = new HostServer(w, Profile.Id);
            var t = Host.ConnectLocal();
            Client = new GameClient(t);
            Hook(Client);
            Client.Hello(Profile.Id, Settings.PlayerName, Host.Session.Code, Profile.CosmeticsJson(), null, true);
            Mode = AppMode.Loading;
            LoadingText = "Welt wird geladen …";
            loadingTimeout = 20f;
            UIState.Open(UIScreen.Loading);
        }

        /// <summary>Öffnet die laufende Welt für Mitspieler (TCP-Port aus den Einstellungen).</summary>
        public bool OpenCoop(out string invite, out string error)
        {
            invite = null; error = null;
            if (Host == null) { error = "Nur der Host kann eine Sitzung öffnen."; return false; }
            if (!Host.OpenOnline(Settings.CoopPort, out error)) return false;
            invite = Host.InviteText(HostServer.LocalAddresses()[0]);
            return true;
        }

        public void CloseCoop()
        {
            if (Host == null) return;
            Host.CloseOnline();
            Hud.Show("Koop geschlossen – die Welt ist wieder privat.");
        }

        public List<string> InviteTexts()
        {
            var l = new List<string>();
            if (Host == null || !Host.Online) return l;
            foreach (var a in HostServer.LocalAddresses()) l.Add(Host.InviteText(a));
            return l;
        }

        /// <summary>Tritt einer Sitzung bei. Einladung: „Adresse:Port/CODE“ (oder Adresse + separater Code).</summary>
        public bool Join(string invite, string code, out string error)
        {
            error = null;
            string host; int port; string parsedCode;
            if (!HostServer.ParseInvite(invite, out host, out port, out parsedCode)) { error = "Adresse ungültig. Format: 192.168.0.10:7777/ABC123"; return false; }
            string c = string.IsNullOrEmpty(code) ? parsedCode : code.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(c)) { error = "Bitte den Sitzungscode angeben."; return false; }
            EndSession(false);
            var t = new TcpClientTransport();
            try { t.Connect(host, port); }
            catch (Exception e) { error = "Verbindung nicht möglich: " + e.Message; return false; }
            Client = new GameClient(t);
            Hook(Client);
            string token;
            Profile.Tokens.TryGetValue(c, out token);
            Client.Hello(Profile.Id, Settings.PlayerName, c, Profile.CosmeticsJson(), token);
            joinCode = c;
            Settings.LastJoin = invite;
            Settings.Save();
            Mode = AppMode.Loading;
            LoadingText = "Verbinde mit " + host + ":" + port + " …";
            loadingTimeout = 20f;
            UIState.Open(UIScreen.Loading);
            return true;
        }

        /// <summary>Zurück ins Hauptmenü. Der Host speichert vorher; Gäste werden mit Meldung zurückgeführt.</summary>
        public void LeaveToMenu(string messageTitle = null, string message = null)
        {
            EndSession(true);
            Mode = AppMode.Menu;
            if (message != null) UIState.Message(messageTitle ?? "Hinweis", message, UIScreen.MainMenu);
            else UIState.Open(UIScreen.MainMenu);
        }

        void EndSession(bool save)
        {
            bool had = Client != null || Host != null;
            if (Host != null)
            {
                if (save && Client != null && Client.Joined) SaveNow(null, true);
                Host.Shutdown("Der Host hat die Sitzung beendet. Die Welt wurde beim Host gesichert.");
                Host = null;
            }
            if (Client != null)
            {
                Client.Leave();
                Client.T.Close();
                Client = null;
            }
            PhotoMode.Active = false;
            BuildMode.Active = false;
            Hud.ClearTransient();
            if (had) OnSessionEnded?.Invoke();
        }

        /// <summary>Speichert die Welt (nur Host). slot = null → aktueller Slot.</summary>
        public bool SaveNow(string slot = null, bool silent = false)
        {
            if (Host == null) { if (!silent) Hud.Show("Die Welt gehört dem Host – nur der Host kann speichern.", ToastKind.Warning); return false; }
            string err;
            bool ok = Saves.Save(slot ?? Slot, Host.Session.Game.S, out err);
            if (!ok) Hud.Show(err, ToastKind.Error, 6f);
            else if (!silent) Hud.Show("Gespeichert (" + SlotName(slot ?? Slot) + ").", ToastKind.Success, 2f);
            savedFlash = 1.5f;
            return ok;
        }

        public float savedFlash;

        public static string SlotName(string slot)
        {
            switch (slot)
            {
                case "auto": return "Automatisch";
                case "slot1": return "Spielstand 1";
                case "slot2": return "Spielstand 2";
                case "slot3": return "Spielstand 3";
            }
            return slot;
        }

        public void QuitGame()
        {
            EndSession(true);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Act(JObj a, Action<ActResult> cb = null)
        {
            if (Client == null || !Client.Joined) return;
            Client.Act(a, cb);
        }

        // ================================================================== Ereignisse
        void Hook(GameClient c)
        {
            c.Fx += f =>
            {
                FxToast(f);
                try { OnFx?.Invoke(f); } catch (Exception e) { Debug.LogException(e); }
            };
            c.SaveReceived += (text, reason) =>
            {
                if (Host == null || text == null) return;
                string err;
                if (!Saves.SaveText(Slot, text, out err)) Hud.Show(err, ToastKind.Error, 6f);
                else savedFlash = 1.5f;
            };
            c.Notice += msg => Hud.Show(msg, ToastKind.Warning, 5f);
            c.ActionFailed += (r, a) => { if (!string.IsNullOrEmpty(r.Err)) Hud.Show(r.Err, ToastKind.Warning, 3.5f); };
            c.PlanetChanged += p => { try { OnPlanetChanged?.Invoke(p); } catch (Exception e) { Debug.LogException(e); } };
            c.Emote += (pid, e) => { };
        }

        string PlayerName(string pid)
        {
            PlayerData p;
            return W != null && pid != null && W.Players.TryGetValue(pid, out p) ? p.Name : "Jemand";
        }

        bool IsMe(JObj f) { return Client != null && f.Str("pid") == Client.Pid; }

        void FxToast(JObj f)
        {
            string name = f.Str("name");
            switch (f.Str("k"))
            {
                case "zone": Hud.Show("✦ Lichtpunkt „" + name + "“ ist wieder sauber!", ToastKind.Success, 4f); break;
                case "gate": Hud.Show("Der Weg nach „" + f.Str("area") + "“ ist frei!", ToastKind.Success, 5f); break;
                case "areaclean": Hud.Show(name + ": Hauptmüll entfernt! Jetzt das Projekt am Projektplatz starten.", ToastKind.Success, 6f); break;
                case "projectstart": Hud.Show("Bau gestartet: „" + name + "“.", ToastKind.Info); break;
                case "awaken": Hud.Show((f.Bool("great") ? "GROSSPROJEKT abgeschlossen: „" : "Projekt abgeschlossen: „") + name + "“ – die Umgebung erwacht!", ToastKind.Story, 7f); break;
                case "unlock": Hud.Show(name + " ist jetzt erreichbar!", ToastKind.Story, 6f); break;
                case "mission_done": Hud.Show("Auftrag erledigt: " + f.Str("title") + (f.Int("reward") > 0 ? " (+" + f.Int("reward") + " Credits)" : ""), ToastKind.Success, 4.5f); break;
                case "mission_new": Hud.Show("Neuer Auftrag: " + f.Str("title"), ToastKind.Info, 4f); break;
                case "cosmetic":
                    Hud.Show("Kosmetik freigeschaltet: " + name, ToastKind.Success, 4f);
                    if (Profile.Unlocks.Add(f.Str("id"))) Profile.Save();
                    break;
                case "stormwarn": Hud.Show((name ?? "Ein Sturm") + " zieht auf! Suche einen Unterschlupf.", ToastKind.Warning, 6f); break;
                case "storm":
                    if (f.Bool("on")) Hud.Show((name ?? "Sturm") + "! Ohne Unterschlupf leert sich der Akku schnell.", ToastKind.Warning, 6f);
                    else Hud.Show(f.Bool("dunes") ? "Der " + name + " ist vorbei – die Dünen haben sich verschoben, neue Wege sind frei." : "Der " + name + " ist vorbei.", ToastKind.Info, 5f);
                    break;
                case "nightfall": Hud.Show("Die Nacht bricht herein. Suche einen Unterschlupf und schlafe [" + InputMap.Label(GameAction.Sleep) + "].", ToastKind.Warning, 7f); break;
                case "daybreak": Hud.Show("Ein neuer Morgen bricht an.", ToastKind.Info, 3f); break;
                case "morning": Hud.Show("Ausgeschlafen! Ein neuer Morgen – Akku voll.", ToastKind.Success, 4f); break;
                case "shutdown":
                    if (IsMe(f)) Hud.Show("Notabschaltung! Eine Abschleppdrohne bringt MIKO zum Stützpunkt …", ToastKind.Error, 6f);
                    else Hud.Show(PlayerName(f.Str("pid")) + " hatte eine Notabschaltung und wird abgeschleppt.", ToastKind.Warning);
                    break;
                case "towed": if (IsMe(f)) Hud.Show("Am Stützpunkt angekommen. Der Akku lädt.", ToastKind.Info, 4f); break;
                case "sleep":
                    if (f.Int("n") < f.Int("of")) Hud.Show("Warte auf Mitspieler (" + f.Int("n") + "/" + f.Int("of") + " schlafen) …", ToastKind.Info, 4f);
                    break;
                case "sheltered": Hud.Show("Notunterschlupf gebaut – hier kannst du schlafen.", ToastKind.Success); break;
                case "lore": if (IsMe(f) && GameData.Lore.ContainsKey(f.Str("id") ?? "")) Hud.Show("Fundstück entdeckt: " + GameData.Lore[f.Str("id")].Title + " (Archiv)", ToastKind.Story, 5f); break;
                case "sell": if (IsMe(f)) Hud.Show("+" + f.Long("c") + " Credits", ToastKind.Success, 2.5f); break;
                case "dispose": if (IsMe(f)) Hud.Show("Gefahrstoffe fachgerecht entsorgt: +" + f.Long("c") + " Credits", ToastKind.Success); break;
                case "contract": Hud.Show("Recyclingauftrag erfüllt: +" + f.Long("c") + " Credits", ToastKind.Success); break;
                case "upgrade":
                    {
                        TechDef t;
                        if (GameData.Tech.TryGetValue(f.Str("id") ?? "", out t) && t.Levels.Count > 0) Hud.Show("Eingebaut: " + t.Name + " – " + t.Levels[Mathf.Clamp(f.Int("lvl"), 0, t.MaxLevel)].Label, ToastKind.Success);
                        break;
                    }
                case "vehicle":
                    {
                        VehicleDef vd;
                        if (GameData.Vehicles.TryGetValue(f.Str("id") ?? "", out vd)) Hud.Show("Neues Fahrzeug in der Garage: " + vd.Name, ToastKind.Success);
                        break;
                    }
                case "ship": Hud.Show("Sprungantrieb eingebaut!", ToastKind.Success); break;
                case "build": if (GameData.Buildings.ContainsKey(f.Str("t") ?? "")) Hud.Show("Gebaut: " + GameData.Buildings[f.Str("t")].Name, ToastKind.Success, 2.5f); break;
                case "join": if (!IsMe(f)) Hud.Show(name + " ist der Sitzung beigetreten.", ToastKind.Info); break;
                case "leave": if (!IsMe(f)) Hud.Show(name + " hat die Sitzung verlassen.", ToastKind.Info); break;
                case "delivery": Hud.Show("Schrottlieferung am Abladeplatz eingetroffen (" + f.Int("n") + " Teile).", ToastKind.Info); break;
                case "wreckdone": Hud.Show("Wrack verwertet: +" + f.Int("units") + " Einheiten im Lager.", ToastKind.Success); break;
                case "repaired": Hud.Show("Repariert! (+15 Credits)", ToastKind.Success, 2.5f); break;
                case "eco": Hud.Show(name + ": Ökologie wiederhergestellt – hier lebt es wieder!", ToastKind.Story, 6f); break;
                case "lifted": Hud.Show(f.Int("help") > 0 ? "Wrack angehoben – mit " + f.Int("help") + " Helfern!" : "Wrack angehoben. Auf den Transporter setzen oder zum Stützpunkt fahren.", ToastKind.Success); break;
                case "onrover": Hud.Show("Wrack liegt auf dem Transportrover – ab zum Stützpunkt!", ToastKind.Success); break;
                case "arrive":
                    {
                        PlanetDef pd;
                        if (GameData.Planets.TryGetValue(f.Str("planet") ?? "", out pd)) Hud.Show(pd.Name + " – " + pd.Subtitle, ToastKind.Story, 5f);
                        break;
                    }
                case "ending":
                    if (OnEndingRequested != null)
                    {
                        Mode = AppMode.Ending;
                        UIState.Open(UIScreen.Ending);
                        OnEndingRequested(() =>
                        {
                            Mode = AppMode.Playing;
                            UIState.Open(UIScreen.None);
                            Act(new JObj().Set("a", "endingSeen"));
                            Hud.Show("Freies Spiel: Die Welten gehören jetzt dir. Recyclingaufträge und Lieferungen bleiben verfügbar.", ToastKind.Story, 8f);
                        });
                    }
                    break;
            }
        }

        // ================================================================== Schleife
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (savedFlash > 0) savedFlash -= dt;
            try
            {
                if (Host != null) Host.Update(dt, !Paused);
                if (Client != null) Client.Update(dt);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Hud.Show("Interner Fehler: " + e.Message, ToastKind.Error, 8f);
            }
            if (Client == null) return;

            if (Mode == AppMode.Loading)
            {
                if (Client.Joined)
                {
                    Mode = AppMode.Playing;
                    UIState.Open(UIScreen.None);
                    if (joinCode != null && Client.Token != null) { Profile.Tokens[joinCode] = Client.Token; Profile.Save(); }
                    joinCode = null;
                    try { OnSessionStarted?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                    if (IsGuest && !Settings.IntroSeenOnce && OnIntroRequested != null) PlayGuestIntro();
                    // Abspann nachholen, falls das Spiel während des Abspanns beendet wurde
                    else if (IsHost && W != null && !W.EndingSeen && W.CampaignDone) FxToast(new JObj().Set("k", "ending"));
                }
                else
                {
                    loadingTimeout -= dt;
                    if (Client.FatalError != null || loadingTimeout <= 0)
                    {
                        string err = Client.FatalError ?? "Zeitüberschreitung – keine Antwort vom Host.";
                        EndSession(false);
                        Mode = AppMode.Menu;
                        UIState.Message("Verbindung fehlgeschlagen", err, UIScreen.Coop);
                    }
                }
                return;
            }
            if (Client.FatalError != null || Client.Ended)
            {
                string err = Client.FatalError ?? "Die Sitzung wurde beendet.";
                bool wasGuest = IsGuest;
                EndSession(false);
                Mode = AppMode.Menu;
                UIState.Message(wasGuest ? "Sitzung beendet" : "Hinweis", err, UIScreen.MainMenu);
            }
        }

        void OnApplicationQuit()
        {
            if (Host != null && Client != null && Client.Joined) SaveNow(null, true);
            if (Host != null) Host.Shutdown("Der Host hat das Spiel beendet. Die Welt wurde beim Host gesichert.");
            else if (Client != null) Client.Leave();
        }
    }
}
