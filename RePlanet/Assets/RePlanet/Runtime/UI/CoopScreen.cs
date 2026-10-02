using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Koop: Beitreten (Hauptmenü), Welt öffnen/Einladungen/Spielerliste/Vertrauensmodus (Host), Verbindungsinfo (Gast).</summary>
    public partial class UIRoot
    {
        string coopError;
        string joinInvite, joinCode = "";
        bool joinInit;
        readonly List<PlayerData> coopPlayers = new List<PlayerData>(4);

        void DrawCoop(GameApp app)
        {
            Backdrop(app);
            var r = CenterRect(1180, 880);
            var inner = Window(r, L("Koop") + L(" – gemeinsam aufräumen (1–4 Spieler)"));
            const int key = 301;
            UINav.BeginScroll(key, inner);
            float w = UINav.ScrollWidth(key, inner);
            float y = DrawCoopContent(app, w, 0);
            UINav.EndScroll(y + 10);
        }

        /// <summary>Koop-Inhalt (auch im Spielmenü-Reiter). Zeichnet ab y in lokalen Koordinaten, liefert die Endhöhe.</summary>
        float DrawCoopContent(GameApp app, float w, float y)
        {
            if (!string.IsNullOrEmpty(coopError))
            {
                float eh = UISkin.TextHeight(UISkin.Wrap, L(coopError), w - 30);
                UISkin.RoundRect(new Rect(0, y, w, eh + 20), new Color(UISkin.Bad.r, UISkin.Bad.g, UISkin.Bad.b, 0.2f));
                GUI.Label(new Rect(14, y + 10, w - 28, eh + 4), UISkin.Col(L(coopError), UISkin.Bad), UISkin.Wrap);
                y += eh + 32;
            }
            if (!app.InGame) return CoopJoinSection(app, w, y);
            if (app.IsHost) return CoopHostSection(app, w, y);
            return CoopGuestSection(app, w, y);
        }

        // ------------------------------------------------------------ Hauptmenü: Beitreten / Laden & hosten
        float CoopJoinSection(GameApp app, float w, float y)
        {
            var s = app.Settings;
            if (!joinInit) { joinInit = true; joinInvite = s.LastJoin ?? ""; }
            float half = (w - 30) * 0.5f;

            // Linke Spalte: Beitreten
            float ly = y;
            GUI.Label(new Rect(0, ly, half, 36), L("Sitzung beitreten"), UISkin.H3);
            ly += 42;
            GUI.Label(new Rect(0, ly, half, 26), L("Spielername"), UISkin.LabelSmall);
            ly += 28;
            string nm = UINav.TextField(new Rect(0, ly, half, 44), s.PlayerName, 20, "coop_name");
            if (nm != s.PlayerName) { s.PlayerName = nm; settingsSaveDirty = true; }
            ly += 56;
            GUI.Label(new Rect(0, ly, half, 26), L("Adresse oder Einladung (z. B. 192.168.0.10:7777/ABC123)"), UISkin.LabelSmall);
            ly += 28;
            joinInvite = UINav.TextField(new Rect(0, ly, half - 130, 44), joinInvite, 120, "coop_invite");
            if (UINav.Button(new Rect(half - 120, ly, 120, 44), L("Einfügen"), true, UISkin.ButtonSmall))
            {
                var clip = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clip)) joinInvite = clip.Trim();
            }
            ly += 56;
            GUI.Label(new Rect(0, ly, half, 26), L("Sitzungscode") + L(" (optional, falls nicht in der Einladung)"), UISkin.LabelSmall);
            ly += 28;
            joinCode = UINav.TextField(new Rect(0, ly, 260, 44), joinCode, 12, "coop_code");
            ly += 60;
            if (UINav.Button(new Rect(0, ly, half, 52), L("Verbinden"), !string.IsNullOrEmpty(joinInvite), UISkin.ButtonSel))
            {
                string err;
                coopError = null;
                if (string.IsNullOrEmpty(s.PlayerName)) s.PlayerName = "MIKO";
                s.Save();
                if (!app.Join(joinInvite.Trim(), joinCode, out err)) coopError = err;
            }
            ly += 66;
            string help =
                UISkin.Col(L("So klappt die Verbindung"), UISkin.Accent) + "\n" +
                L("• <b>Im selben Netzwerk (LAN/WLAN):</b> Die Einladung des Hosts einfach einfügen.\n") +
                L("• <b>Über das Internet:</b> Der Host leitet am Router den TCP-Port (Standard 7777) an seinen PC weiter und teilt seine öffentliche IP.\n") +
                L("• <b>Ohne Portweiterleitung:</b> Ein VPN wie Tailscale oder ZeroTier – dann die VPN-Adresse des Hosts nutzen.\n") +
                L("• <b>Dedizierter Server:</b> Adresse und Code des Servers eingeben; die Welt läuft dort weiter, auch wenn der Host offline ist.");
            float hh = UISkin.TextHeight(UISkin.WrapSmall, help, half);
            GUI.Label(new Rect(0, ly, half, hh + 6), help, UISkin.WrapSmall);
            ly += hh + 16;

            // Rechte Spalte: Eigene Welt hosten
            float rx = half + 30, ry = y;
            GUI.Label(new Rect(rx, ry, half, 36), L("Sitzung erstellen"), UISkin.H3);
            ry += 42;
            string hostHelp = L("Lade einen Spielstand – danach wird die Welt automatisch für Mitspieler geöffnet (Port ") + s.CoopPort + L(", änderbar unter Einstellungen → Sonstiges). Die Einladung erscheint anschließend hier.");
            float hh2 = UISkin.TextHeight(UISkin.WrapSmall, hostHelp, half);
            GUI.Label(new Rect(rx, ry, half, hh2 + 4), hostHelp, UISkin.WrapSmall);
            ry += hh2 + 12;
            if (savesCache.Count == 0) RefreshSaves();
            bool any = false;
            foreach (var info in savesCache)
            {
                if (info.World == null) continue;
                any = true;
                var row = new Rect(rx, ry, half, 64);
                UISkin.PanelBoxLight(row);
                GUI.Label(new Rect(rx + 14, ry + 4, half - 200, 30), info.World + UISkin.Col("  · " + L(GameApp.SlotName(info.Slot)), UISkin.TextDim), UISkin.LabelBold);
                GUI.Label(new Rect(rx + 14, ry + 32, half - 200, 26), info.Planet + " · " + FormatTime(info.Playtime) + " · " + (info.Restoration * 100).ToString("0") + " %", UISkin.LabelSmall);
                if (UINav.Button(new Rect(rx + half - 184, ry + 12, 170, 40), L("Laden & hosten"), true, UISkin.ButtonSmall))
                {
                    coopError = null;
                    autoOpenCoop = true;
                    autoOpenTimeout = 30f;
                    app.Continue(info.Slot);
                }
                ry += 72;
            }
            if (!any)
            {
                GUI.Label(new Rect(rx, ry, half, 30), UISkin.Col(L("Noch kein Spielstand vorhanden – starte zuerst ein neues Spiel."), UISkin.TextDim), UISkin.LabelSmall);
                ry += 40;
                if (UINav.Button(new Rect(rx, ry, half, 46), L("Neues Spiel"))) UIState.Open(UIScreen.NewGame);
                ry += 56;
            }
            return Mathf.Max(ly, ry);
        }

        // ------------------------------------------------------------ Host im Spiel
        float CoopHostSection(GameApp app, float w, float y)
        {
            var W = app.W;
            if (!app.CoopOpen)
            {
                string t = L("Deine Welt ist privat. Öffne sie, damit bis zu ") + (GameData.MaxPlayers - 1) + L(" Freunde beitreten können. Die Simulation läuft bei dir – du speicherst für alle.");
                float th = UISkin.TextHeight(UISkin.Wrap, t, w);
                GUI.Label(new Rect(0, y, w, th + 4), t, UISkin.Wrap);
                y += th + 14;
                if (UINav.Button(new Rect(0, y, 420, 54), L("Welt für Mitspieler öffnen"), true, UISkin.ButtonSel))
                {
                    string inv, err;
                    coopError = null;
                    if (app.OpenCoop(out inv, out err)) { Hud.Show(L("Koop geöffnet – Einladung teilen!"), ToastKind.Success, 4f); AudioManager.Ui("ui_click"); }
                    else coopError = L("Koop konnte nicht geöffnet werden: ") + err;
                }
                GUI.Label(new Rect(440, y, w - 440, 54), L("TCP-Port ") + app.Settings.CoopPort + L(" (Einstellungen → Sonstiges)"), UISkin.LabelSmall);
                y += 70;
            }
            else
            {
                GUI.Label(new Rect(0, y, w, 34), UISkin.Col(L("● Koop ist offen"), UISkin.Good) + L("  –  Port ") + app.Host.Port + L(" · Code ") + UISkin.Col(app.Host.Session.Code, UISkin.Accent), UISkin.LabelBold);
                y += 42;
                GUI.Label(new Rect(0, y, w, 28), L("Einladungen (je nach Netzwerk die passende Adresse teilen):"), UISkin.LabelSmall);
                y += 32;
                var invites = app.InviteTexts();
                foreach (var inv in invites)
                {
                    var row = new Rect(0, y, w, 46);
                    UISkin.PanelBoxLight(row);
                    GUI.Label(new Rect(16, y, w - 200, 46), inv, UISkin.Mono);
                    if (UINav.Button(new Rect(w - 170, y + 5, 160, 36), L("Kopieren"), true, UISkin.ButtonSmall))
                    {
                        GUIUtility.systemCopyBuffer = inv;
                        Hud.Show(L("Einladung kopiert: ") + inv, ToastKind.Info, 3f);
                    }
                    y += 52;
                }
                string net = L("Gleiches Netzwerk: lokale Adresse (192.168… / 10…). Internet: öffentliche IP des Routers mit weitergeleitetem TCP-Port ") + app.Host.Port + ", z. B. „" + "IP:" + app.Host.Port + "/" + app.Host.Session.Code + L("“. Mit Tailscale/ZeroTier: die VPN-Adresse (100… bzw. je nach Netz).");
                float nh = UISkin.TextHeight(UISkin.WrapSmall, net, w);
                GUI.Label(new Rect(0, y, w, nh + 4), net, UISkin.WrapSmall);
                y += nh + 12;
                if (confirm == "closecoop")
                {
                    GUI.Label(new Rect(0, y, 420, 44), UISkin.Col(L("Alle Mitspieler werden getrennt. Sicher?"), UISkin.Warn), UISkin.LabelBold);
                    if (UINav.Button(new Rect(430, y, 200, 44), L("Ja, schließen"), true, UISkin.ButtonSel)) { confirm = null; app.CloseCoop(); }
                    if (UINav.Button(new Rect(640, y, 200, 44), L("Abbrechen"), true, UISkin.ButtonSmall)) confirm = null;
                }
                else if (UINav.Button(new Rect(0, y, 300, 44), L("Koop schließen"), true, UISkin.ButtonSmall))
                {
                    if (app.Host.Session.OnlineCount > 1) confirm = "closecoop"; else app.CloseCoop();
                }
                y += 58;
            }

            // Vertrauensmodus
            bool trust = UINav.Toggle(new Rect(0, y, w, 46), W.TrustGuests, L("Vertrauensmodus: Gäste dürfen teure Käufe (ab ") + Num(GameData.GuestExpensiveThreshold) + L(" Credits), Abriss und Reisen auslösen"));
            if (trust != W.TrustGuests) app.Act(new JObj().Set("a", "trust").Set("v", trust));
            y += 56;
            // TNT trifft Mitspieler (mit der Welt gespeichert)
            bool hits = UINav.Toggle(new Rect(0, y, w, 46), W.TntHitsPlayers, L("TNT trifft Mitspieler: Getroffene fliegen harmlos durch die Luft (aus = nur der Werfer selbst)"));
            if (hits != W.TntHitsPlayers) app.Act(new JObj().Set("a", "tnthits").Set("v", hits));
            y += 56;
            return CoopPlayerList(app, w, y);
        }

        // ------------------------------------------------------------ Gast im Spiel
        float CoopGuestSection(GameApp app, float w, float y)
        {
            var c = app.Client;
            var W = app.W;
            string host = "?";
            PlayerData hp;
            if (c != null && c.HostPid != null && W.Players.TryGetValue(c.HostPid, out hp)) host = hp.Name;
            GUI.Label(new Rect(0, y, w, 34), UISkin.Col(L("● Verbunden"), UISkin.Good) + L(" mit der Welt „") + W.WorldName + L("“ von ") + host + (c != null && c.Dedicated ? L(" (dedizierter Server)") : ""), UISkin.LabelBold);
            y += 40;
            float ping = c != null ? c.PingMs : 0f;
            Color pc = ping < 80 ? UISkin.Good : ping < 180 ? UISkin.Warn : UISkin.Bad;
            GUI.Label(new Rect(0, y, w, 30), L("Ping: ") + UISkin.Col(ping.ToString("0") + L(" ms"), pc) + (c != null && c.Code != null ? L("   ·   Code ") + c.Code : ""), UISkin.Label);
            y += 36;
            string t = L("Die Welt gehört dem Host: Er speichert für alle. ") + (W.TrustGuests
                ? L("Vertrauensmodus ist an – du darfst auch teure Käufe tätigen.")
                : L("Teure Käufe (ab ") + Num(GameData.GuestExpensiveThreshold) + L(" Credits), Abriss und Reisen sind dem Host vorbehalten."));
            float th = UISkin.TextHeight(UISkin.WrapSmall, t, w);
            GUI.Label(new Rect(0, y, w, th + 4), t, UISkin.WrapSmall);
            y += th + 14;
            y = CoopPlayerList(app, w, y);
            y += 10;
            if (confirm == "leave")
            {
                GUI.Label(new Rect(0, y, 380, 44), UISkin.Col(L("Sitzung wirklich verlassen?"), UISkin.Warn), UISkin.LabelBold);
                if (UINav.Button(new Rect(390, y, 200, 44), L("Ja, verlassen"), true, UISkin.ButtonSel)) { confirm = null; app.LeaveToMenu(); }
                if (UINav.Button(new Rect(600, y, 200, 44), L("Abbrechen"), true, UISkin.ButtonSmall)) confirm = null;
            }
            else if (UINav.Button(new Rect(0, y, 300, 44), L("Sitzung verlassen"), true, UISkin.ButtonSmall)) confirm = "leave";
            return y + 56;
        }

        float CoopPlayerList(GameApp app, float w, float y)
        {
            var W = app.W;
            GUI.Label(new Rect(0, y, w, 34), L("Spieler"), UISkin.H3);
            y += 40;
            coopPlayers.Clear();
            foreach (var p in W.Players.Values) coopPlayers.Add(p);
            coopPlayers.Sort((a, b) => (b.Online ? 1 : 0).CompareTo(a.Online ? 1 : 0));
            string hostPid = app.Client != null ? app.Client.HostPid : null;
            foreach (var p in coopPlayers)
            {
                var row = new Rect(0, y, w, 40);
                UISkin.RoundRect(row, new Color(1, 1, 1, 0.04f));
                UISkin.Tex(new Rect(12, y + 12, 16, 16), UISkin.Circle, p.Online ? UISkin.Good : UISkin.TextDim * new Color(1, 1, 1, 0.5f));
                CosmeticDef cd;
                Color col = GameData.Cosmetics.TryGetValue(p.Color ?? "", out cd) ? UISkin.FromRgb(cd.Value) : UISkin.Teal;
                UISkin.Tex(new Rect(36, y + 8, 24, 24), UISkin.Shape("dot"), col);
                string label = p.Name + (p.Id == hostPid ? UISkin.Col(L("  (Host)"), UISkin.Accent) : "") + (app.Client != null && p.Id == app.Client.Pid ? UISkin.Col(L("  (du)"), UISkin.Teal) : "");
                GUI.Label(new Rect(70, y, w * 0.5f, 40), label, UISkin.Label);
                string status = !p.Online ? L("offline") : p.TowTimer > 0 ? L("wird abgeschleppt") : p.Sleeping ? L("schläft") : p.Vehicle != null && GameData.Vehicles.ContainsKey(p.Vehicle) ? L("fährt ") + GameData.Vehicles[p.Vehicle].Name : "aktiv";
                GUI.Label(new Rect(w * 0.55f, y, w * 0.45f - 10, 40), UISkin.Col(status, p.Online ? UISkin.TextDim : UISkin.TextDim * new Color(1, 1, 1, 0.6f)), UISkin.LabelSmall);
                y += 44;
            }
            return y;
        }
    }
}
