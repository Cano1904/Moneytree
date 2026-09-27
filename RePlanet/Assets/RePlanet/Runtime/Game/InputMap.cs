using System;
using System.Collections.Generic;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Spielaktionen – frei belegbar (Tastatur/Maus) plus feste Controller-Belegung.</summary>
    public enum GameAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Sprint,
        Interact, UseTool, AltTool, ToolNext, ToolPrev,
        Tool1, Tool2, Tool3, Tool4, Tool5, Tool6, Tool7,
        Press, Menu, Map, Build, Photo, Pause, DiveUp, DiveDown,
        Vehicle, VehicleReset, Emote, Sleep, Shelter, Missions, Inventory, QuickSave, RotateBuild
    }

    /// <summary>
    /// Eingabe über Unitys klassischen Input Manager (Tastatur, Maus, XInput-Controller).
    /// Controller-Achsen werden in ProjectSettings/InputManager.asset als RP_* definiert.
    /// Fehlen sie, wird der Controller einfach deaktiviert statt einen Fehler zu werfen.
    /// </summary>
    public static class InputMap
    {
        public static readonly Dictionary<GameAction, KeyCode> Defaults = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.MoveForward, KeyCode.W }, { GameAction.MoveBack, KeyCode.S }, { GameAction.MoveLeft, KeyCode.A }, { GameAction.MoveRight, KeyCode.D },
            { GameAction.Sprint, KeyCode.LeftShift }, { GameAction.Interact, KeyCode.E }, { GameAction.UseTool, KeyCode.Mouse0 }, { GameAction.AltTool, KeyCode.Mouse1 },
            { GameAction.ToolNext, KeyCode.None }, { GameAction.ToolPrev, KeyCode.None },
            { GameAction.Tool1, KeyCode.Alpha1 }, { GameAction.Tool2, KeyCode.Alpha2 }, { GameAction.Tool3, KeyCode.Alpha3 }, { GameAction.Tool4, KeyCode.Alpha4 },
            { GameAction.Tool5, KeyCode.Alpha5 }, { GameAction.Tool6, KeyCode.Alpha6 }, { GameAction.Tool7, KeyCode.Alpha7 },
            { GameAction.Press, KeyCode.R }, { GameAction.Menu, KeyCode.Tab }, { GameAction.Map, KeyCode.M }, { GameAction.Build, KeyCode.B },
            { GameAction.Photo, KeyCode.P }, { GameAction.Pause, KeyCode.Escape }, { GameAction.DiveUp, KeyCode.Space }, { GameAction.DiveDown, KeyCode.C },
            { GameAction.Vehicle, KeyCode.F }, { GameAction.VehicleReset, KeyCode.X }, { GameAction.Emote, KeyCode.G }, { GameAction.Sleep, KeyCode.Z },
            { GameAction.Shelter, KeyCode.N }, { GameAction.Missions, KeyCode.J }, { GameAction.Inventory, KeyCode.I }, { GameAction.QuickSave, KeyCode.F5 },
            { GameAction.RotateBuild, KeyCode.R },
        };

        public static readonly Dictionary<GameAction, string> Names = new Dictionary<GameAction, string>
        {
            { GameAction.MoveForward, "Vorwärts" }, { GameAction.MoveBack, "Rückwärts" }, { GameAction.MoveLeft, "Links" }, { GameAction.MoveRight, "Rechts" },
            { GameAction.Sprint, "Sprinten" }, { GameAction.Interact, "Interagieren" }, { GameAction.UseTool, "Werkzeug benutzen" }, { GameAction.AltTool, "Magnet aufladen / Zweitfunktion" },
            { GameAction.ToolNext, "Nächstes Werkzeug" }, { GameAction.ToolPrev, "Vorheriges Werkzeug" },
            { GameAction.Tool1, "Greifarm" }, { GameAction.Tool2, "Müllsauger" }, { GameAction.Tool3, "Magnetarm" }, { GameAction.Tool4, "Schneidgerät" },
            { GameAction.Tool5, "Wärmemodul" }, { GameAction.Tool6, "Filtermodul" }, { GameAction.Tool7, "Bio-Modul" },
            { GameAction.Press, "Pressen" }, { GameAction.Menu, "Spielmenü" }, { GameAction.Map, "Karte" }, { GameAction.Build, "Bauansicht" },
            { GameAction.Photo, "Fotomodus" }, { GameAction.Pause, "Pause" }, { GameAction.DiveUp, "Auftauchen" }, { GameAction.DiveDown, "Abtauchen" },
            { GameAction.Vehicle, "Ein-/Aussteigen" }, { GameAction.VehicleReset, "Fahrzeug zurücksetzen" }, { GameAction.Emote, "Roboterlaut" }, { GameAction.Sleep, "Schlafen" },
            { GameAction.Shelter, "Notunterschlupf bauen" }, { GameAction.Missions, "Aufträge" }, { GameAction.Inventory, "Inventar" }, { GameAction.QuickSave, "Schnellspeichern" },
            { GameAction.RotateBuild, "Bauwerk drehen" },
        };

        static readonly Dictionary<GameAction, KeyCode> bindings = new Dictionary<GameAction, KeyCode>(Defaults);
        static bool padAxesOk = true, checkedAxes;
        public static bool UsingPad { get; private set; }
        public static float PadDeadzone = 0.2f;

        // Controller (XInput unter Windows): A=0 B=1 X=2 Y=3 LB=4 RB=5 Back=6 Start=7 LS=8 RS=9
        static KeyCode PadButton(GameAction a)
        {
            switch (a)
            {
                case GameAction.Interact: return KeyCode.JoystickButton0;
                case GameAction.Vehicle: return KeyCode.JoystickButton3;
                case GameAction.Press: return KeyCode.JoystickButton2;
                case GameAction.ToolPrev: return KeyCode.JoystickButton4;
                case GameAction.ToolNext: return KeyCode.JoystickButton5;
                case GameAction.Map: return KeyCode.JoystickButton6;
                case GameAction.Pause: return KeyCode.JoystickButton7;
                case GameAction.Sprint: return KeyCode.JoystickButton8;
                case GameAction.Photo: return KeyCode.JoystickButton9;
                case GameAction.DiveUp: return KeyCode.JoystickButton0;
                default: return KeyCode.None;
            }
        }

        public static KeyCode Get(GameAction a) { KeyCode k; return bindings.TryGetValue(a, out k) ? k : KeyCode.None; }

        public static void Rebind(GameAction a, KeyCode k)
        {
            // Doppelbelegung vermeiden: bisherige Aktion mit dieser Taste wird frei
            if (k != KeyCode.None)
                foreach (var kv in new List<KeyValuePair<GameAction, KeyCode>>(bindings))
                    if (kv.Value == k && kv.Key != a && !(IsBuildOnly(kv.Key) || IsBuildOnly(a))) bindings[kv.Key] = KeyCode.None;
            bindings[a] = k;
        }

        static bool IsBuildOnly(GameAction a) { return a == GameAction.RotateBuild; }

        public static void ResetDefaults() { bindings.Clear(); foreach (var kv in Defaults) bindings[kv.Key] = kv.Value; }

        public static void LoadBindings(Dictionary<string, string> saved)
        {
            ResetDefaults();
            if (saved == null) return;
            foreach (var kv in saved)
            {
                GameAction a; KeyCode k;
                if (Enum.TryParse(kv.Key, out a) && Enum.TryParse(kv.Value, out k)) bindings[a] = k;
            }
        }

        public static Dictionary<string, string> SaveBindings()
        {
            var d = new Dictionary<string, string>();
            foreach (var kv in bindings) d[kv.Key.ToString()] = kv.Value.ToString();
            return d;
        }

        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "—";
                case KeyCode.Mouse0: return "Linke Maustaste";
                case KeyCode.Mouse1: return "Rechte Maustaste";
                case KeyCode.Mouse2: return "Mittlere Maustaste";
                case KeyCode.LeftShift: return "Umschalt";
                case KeyCode.LeftControl: return "Strg";
                case KeyCode.Space: return "Leertaste";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Return: return "Eingabe";
            }
            var s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            return s;
        }

        /// <summary>Anzeige-Text für eine Aktion, z. B. „E“ oder „A-Taste“ je nach zuletzt benutztem Gerät.</summary>
        public static string Label(GameAction a)
        {
            if (UsingPad)
            {
                if (a == GameAction.UseTool) return "RT";
                if (a == GameAction.AltTool) return "LT";
                if (a == GameAction.Menu) return "Steuerkreuz ↑";
                if (a == GameAction.Build) return "Steuerkreuz →";
                if (a == GameAction.Sleep) return "Steuerkreuz ↓";
                if (a == GameAction.DiveDown) return "B";
                var pb = PadButton(a);
                switch (pb)
                {
                    case KeyCode.JoystickButton0: return "A";
                    case KeyCode.JoystickButton1: return "B";
                    case KeyCode.JoystickButton2: return "X";
                    case KeyCode.JoystickButton3: return "Y";
                    case KeyCode.JoystickButton4: return "LB";
                    case KeyCode.JoystickButton5: return "RB";
                    case KeyCode.JoystickButton6: return "Back";
                    case KeyCode.JoystickButton7: return "Start";
                    case KeyCode.JoystickButton8: return "L3";
                    case KeyCode.JoystickButton9: return "R3";
                }
            }
            return KeyName(Get(a));
        }

        public static float Axis(string name)
        {
            if (!padAxesOk && name.StartsWith("RP_")) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch (ArgumentException)
            {
                if (name.StartsWith("RP_")) padAxesOk = false;
                return 0f;
            }
        }

        static float prevLT, prevRT, prevDX, prevDY;
        static float curLT, curRT, curDX, curDY;
        static int frame = -1;

        static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!checkedAxes) { checkedAxes = true; Axis("RP_LX"); }
            prevLT = curLT; prevRT = curRT; prevDX = curDX; prevDY = curDY;
            curLT = Axis("RP_LT"); curRT = Axis("RP_RT"); curDX = Axis("RP_DX"); curDY = Axis("RP_DY");
            if (Input.anyKeyDown || Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.5f) UsingPad = false;
            if (Mathf.Abs(Axis("RP_LX")) > 0.5f || Mathf.Abs(Axis("RP_LY")) > 0.5f || Mathf.Abs(Axis("RP_RX")) > 0.5f || curLT > 0.5f || curRT > 0.5f) UsingPad = true;
            for (int b = 0; b < 10; b++) if (Input.GetKeyDown(KeyCode.JoystickButton0 + b)) UsingPad = true;
        }

        static bool PadHeld(GameAction a)
        {
            Poll();
            switch (a)
            {
                case GameAction.UseTool: return curRT > 0.5f;
                case GameAction.AltTool: return curLT > 0.5f;
                case GameAction.Menu: return curDY > 0.5f;
                case GameAction.Sleep: return curDY < -0.5f;
                case GameAction.Build: return curDX > 0.5f;
                case GameAction.Emote: return curDX < -0.5f;
                case GameAction.DiveDown: return Input.GetKey(KeyCode.JoystickButton1);
            }
            var k = PadButton(a);
            return k != KeyCode.None && Input.GetKey(k);
        }

        static bool PadDown(GameAction a)
        {
            Poll();
            switch (a)
            {
                case GameAction.UseTool: return curRT > 0.5f && prevRT <= 0.5f;
                case GameAction.AltTool: return curLT > 0.5f && prevLT <= 0.5f;
                case GameAction.Menu: return curDY > 0.5f && prevDY <= 0.5f;
                case GameAction.Sleep: return curDY < -0.5f && prevDY >= -0.5f;
                case GameAction.Build: return curDX > 0.5f && prevDX <= 0.5f;
                case GameAction.Emote: return curDX < -0.5f && prevDX >= -0.5f;
                case GameAction.DiveDown: return Input.GetKeyDown(KeyCode.JoystickButton1);
            }
            var k = PadButton(a);
            return k != KeyCode.None && Input.GetKeyDown(k);
        }

        static bool PadUp(GameAction a)
        {
            Poll();
            switch (a)
            {
                case GameAction.UseTool: return curRT <= 0.5f && prevRT > 0.5f;
                case GameAction.AltTool: return curLT <= 0.5f && prevLT > 0.5f;
            }
            var k = PadButton(a);
            return k != KeyCode.None && Input.GetKeyUp(k);
        }

        public static bool Held(GameAction a) { var k = Get(a); return (k != KeyCode.None && Input.GetKey(k)) || PadHeld(a); }
        public static bool Down(GameAction a) { var k = Get(a); return (k != KeyCode.None && Input.GetKeyDown(k)) || PadDown(a); }
        public static bool Up(GameAction a) { var k = Get(a); return (k != KeyCode.None && Input.GetKeyUp(k)) || PadUp(a); }

        /// <summary>Bewegung (x = rechts, y = vorwärts), Länge ≤ 1.</summary>
        public static Vector2 Move()
        {
            float x = 0, y = 0;
            if (Held(GameAction.MoveRight)) x += 1;
            if (Held(GameAction.MoveLeft)) x -= 1;
            if (Held(GameAction.MoveForward)) y += 1;
            if (Held(GameAction.MoveBack)) y -= 1;
            float px = Axis("RP_LX"), py = -Axis("RP_LY");
            if (Mathf.Abs(px) > PadDeadzone || Mathf.Abs(py) > PadDeadzone) { x += px; y += py; }
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1 ? v.normalized : v;
        }

        /// <summary>Kamerabewegung pro Frame (Maus) bzw. pro Sekunde skaliert (Controller).</summary>
        public static Vector2 Look(float mouseSens, float padSens, bool invertY)
        {
            float mx = 0, my = 0;
            try { mx = Input.GetAxis("Mouse X"); my = Input.GetAxis("Mouse Y"); } catch (ArgumentException) { }
            var v = new Vector2(mx * mouseSens * 2.2f, my * mouseSens * 2.2f);
            float rx = Axis("RP_RX"), ry = -Axis("RP_RY");
            if (Mathf.Abs(rx) > PadDeadzone || Mathf.Abs(ry) > PadDeadzone)
                v += new Vector2(rx, ry) * padSens * 160f * Time.unscaledDeltaTime;
            if (invertY) v.y = -v.y;
            return v;
        }

        public static float Scroll()
        {
            try { return Input.GetAxis("Mouse ScrollWheel"); } catch (ArgumentException) { return 0; }
        }

        // ------------------------------------------------------------ Menü-Navigation (Tastatur + Controller)
        static float navRepeat;
        static Vector2 lastNav;

        /// <summary>Richtungseingabe für Menüs mit Wiederholung beim Halten.</summary>
        public static Vector2Int Nav()
        {
            Poll();
            int x = 0, y = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || curDY > 0.5f && prevDY <= 0.5f) y = 1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || curDY < -0.5f && prevDY >= -0.5f) y = -1;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || curDX < -0.5f && prevDX >= -0.5f) x = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || curDX > 0.5f && prevDX <= 0.5f) x = 1;
            var stick = new Vector2(Axis("RP_LX"), -Axis("RP_LY"));
            if (stick.magnitude > 0.6f)
            {
                navRepeat -= Time.unscaledDeltaTime;
                if (lastNav.magnitude < 0.6f || navRepeat <= 0)
                {
                    navRepeat = lastNav.magnitude < 0.6f ? 0.4f : 0.12f;
                    if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y)) x = stick.x > 0 ? 1 : -1; else y = stick.y > 0 ? 1 : -1;
                }
            }
            lastNav = stick;
            return new Vector2Int(x, y);
        }

        public static bool NavConfirm() { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0); }
        public static bool NavBack() { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1); }
        public static bool NavTabLeft() { return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4); }
        public static bool NavTabRight() { return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5); }

        /// <summary>Wartet auf eine Taste zum Neubelegen (liefert None, solange nichts gedrückt wurde).</summary>
        public static KeyCode CaptureKey()
        {
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k >= KeyCode.JoystickButton0) continue;
                if (Input.GetKeyDown(k)) return k;
            }
            return KeyCode.None;
        }
    }
}
