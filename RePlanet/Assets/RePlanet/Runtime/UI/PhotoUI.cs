using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Fotomodus: ein-/ausblendbares Panel (H), Hochformat-Rahmen, Aussichtspunkte, Aufnahme.</summary>
    public partial class UIRoot
    {
        bool photoPanel = true;

        void DrawPhoto(GameApp app)
        {
            if (!PhotoMode.Active) return;
            bool rep = Event.current.type == EventType.Repaint;

            // Hochformat-Rahmen 9:16
            if (PhotoMode.Portrait && rep)
            {
                float fw = VH * 9f / 16f;
                float fx = (VW - fw) * 0.5f;
                var shade = new Color(0, 0, 0, 0.72f);
                UISkin.Rect(new Rect(0, 0, fx, VH), shade);
                UISkin.Rect(new Rect(fx + fw, 0, VW - fx - fw, VH), shade);
                UISkin.Rect(new Rect(fx - 2, 0, 2, VH), new Color(1, 1, 1, 0.6f));
                UISkin.Rect(new Rect(fx + fw, 0, 2, VH), new Color(1, 1, 1, 0.6f));
            }
            // Drittel-Raster (dezent), nur mit Panel
            if (photoPanel && rep)
            {
                float fw = PhotoMode.Portrait ? VH * 9f / 16f : VW;
                float fx = (VW - fw) * 0.5f;
                var gc = new Color(1, 1, 1, 0.12f);
                UISkin.Rect(new Rect(fx + fw / 3f, 0, 1, VH), gc);
                UISkin.Rect(new Rect(fx + fw * 2f / 3f, 0, 1, VH), gc);
                UISkin.Rect(new Rect(fx, VH / 3f, fw, 1), gc);
                UISkin.Rect(new Rect(fx, VH * 2f / 3f, fw, 1), gc);
            }

            string capKey = InputMap.UsingPad ? "A" : "Leertaste/F12";
            if (!photoPanel)
            {
                string hint = "[H] Panel  ·  [" + capKey + "] Foto  ·  " + KeyHint(GameAction.Photo) + "/Esc Beenden";
                if (InputMap.UsingPad) hint = "[Y] Panel  ·  [A] Foto  ·  [B] Beenden";
                var hr = new Rect((VW - 640) * 0.5f, VH - 54, 640, 38);
                UISkin.RoundRect(hr, new Color(0, 0, 0, 0.45f));
                GUI.Label(hr, hint, UISkin.LabelCenter);
                return;
            }

            var r = new Rect(20, 20, Mathf.Min(440f, VW * 0.36f), VH - 40);
            UISkin.PanelBox(r);
            GUI.Label(new Rect(r.x + 20, r.y + 10, r.width - 40, 40), L("Fotomodus"), UISkin.H2);
            GUI.Label(new Rect(r.x + 20, r.y + 48, r.width - 40, 24), UISkin.Col(InputMap.UsingPad ? "[Y] Panel ausblenden – dann frei umsehen" : "[H] Panel ausblenden – dann mit der Maus umsehen", UISkin.TextDim), UISkin.LabelTiny);
            var view = new Rect(r.x + 14, r.y + 80, r.width - 28, r.height - 94);
            const int key = 601;
            UINav.BeginScroll(key, view);
            float w = UINav.ScrollWidth(key, view);
            float y = 0;
            const float rh = 44f, rs = 50f;

            if (UINav.Button(new Rect(0, y, w, 54), "● Foto aufnehmen", true, UISkin.ButtonSel)) PhotoMode.RequestCapture = true;
            y += 64;
            PhotoMode.HideHud = UINav.Toggle(new Rect(0, y, w, rh), PhotoMode.HideHud, "HUD ausblenden");
            y += rs;
            PhotoMode.Fov = UINav.Slider(new Rect(0, y, w, rh), "Sichtfeld", PhotoMode.Fov, 20f, 100f, 1f, PhotoMode.Fov.ToString("0") + "°");
            y += rs;
            PhotoMode.Roll = UINav.Slider(new Rect(0, y, w, rh), "Neigung", PhotoMode.Roll, -30f, 30f, 1f, PhotoMode.Roll.ToString("0") + "°");
            y += rs;
            PhotoMode.Exposure = UINav.Slider(new Rect(0, y, w, rh), "Belichtung", PhotoMode.Exposure, 0.5f, 2f, 0.05f, PhotoMode.Exposure.ToString("0.00") + "×");
            y += rs;
            PhotoMode.Portrait = UINav.Toggle(new Rect(0, y, w, rh), PhotoMode.Portrait, "Hochformat 9:16");
            y += rs;
            PhotoMode.ShowBefore = UINav.Toggle(new Rect(0, y, w, rh), PhotoMode.ShowBefore, "Vorher-Ansicht (Ausgangszustand)");
            y += rs;
            if (UINav.Button(new Rect(0, y, w, 40), "Werte zurücksetzen", true, UISkin.ButtonSmall))
            {
                PhotoMode.Fov = app.Settings.Fov; PhotoMode.Roll = 0; PhotoMode.Exposure = 1f; PhotoMode.Portrait = false; PhotoMode.ShowBefore = false;
            }
            y += 50;

            if (!string.IsNullOrEmpty(PhotoMode.LastSavedPath))
            {
                GUI.Label(new Rect(0, y, w, 24), "Zuletzt gespeichert:", UISkin.LabelSmall);
                y += 24;
                float ph = UISkin.TextHeight(UISkin.WrapSmall, PhotoMode.LastSavedPath, w);
                GUI.Label(new Rect(0, y, w, ph + 4), PhotoMode.LastSavedPath, UISkin.WrapSmall);
                y += ph + 6;
                if (UINav.Button(new Rect(0, y, w, 36), "Pfad kopieren", true, UISkin.ButtonSmall)) { GUIUtility.systemCopyBuffer = PhotoMode.LastSavedPath; Hud.Show("Pfad kopiert.", ToastKind.Info, 2f); }
                y += 46;
            }

            // Aussichtspunkte
            var w0 = app.W;
            if (w0 != null)
            {
                GUI.Label(new Rect(0, y, w, 30), "Aussichtspunkte", UISkin.H3);
                y += 34;
                var l = WorldGen.Get(w0.CurrentPlanet);
                foreach (var s in l.Viewpoints)
                {
                    bool seen = w0.Cur.Views.Contains(s.Id);
                    if (UINav.Button(new Rect(0, y, w, 40), seen ? "↗ " + s.Name : s.Name + " (noch nicht besucht)", seen, UISkin.ButtonSmall)) PhotoMode.JumpToViewpoint = s.Id;
                    y += 46;
                }
                y = Note("Aussichtspunkte im Gelände besuchen und mit " + KeyHint(GameAction.Interact) + " merken – dann hierher springen.", w, y);
            }

            string help = InputMap.UsingPad
                ? "Linker Stick: bewegen · Rechter Stick: umsehen (Panel aus) · L3: schneller · A: Foto · B: beenden"
                : "WASD: bewegen · Maus: umsehen (Panel aus) · Q/E: runter/hoch · Umschalt: schneller · Leertaste/F12: Foto · " + InputMap.Label(GameAction.Photo) + "/Esc: beenden";
            y = Note(help, w, y);
            y = Note("Fotos landen in: " + app.PhotoDir, w, y);
            if (UINav.Button(new Rect(0, y, w, 46), "Fotomodus beenden")) { ExitPhoto(); AudioManager.Ui("ui_back"); }
            y += 56;
            UINav.EndScroll(y);
        }
    }
}
