using System;
using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// Abschluss der Kampagne: Die Arche-Flotte kehrt zurück (Lichter sinken über dem Stützpunkt herab),
    /// Rückblick auf die vier wiederhergestellten Welten mit echten Werten aus dem Spielstand, danach Mitwirkende.
    /// Anschließend geht das freie Spiel weiter.
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        Action done;
        bool playing;
        float t, skipHold;
        Transform stage;
        readonly List<Transform> ships = new List<Transform>();
        const float Length = 52f;

        void Start() { if (GameApp.I != null) GameApp.I.OnEndingRequested += Play; }

        public void Play(Action onDone)
        {
            done = onDone;
            playing = true; t = 0; skipHold = 0;
            if (CameraRig.I != null) CameraRig.I.Cinematic = true;
            AudioManager.PlayEnding();
            Build();
        }

        void Build()
        {
            if (stage != null) Destroy(stage.gameObject);
            stage = new GameObject("EndingStage").transform;
            ships.Clear();
            var wv = WorldView.I;
            var basePos = wv != null && wv.Layout != null ? new Vector3(wv.Layout.Base.Center.x, wv.Layout.Base.Center.y, wv.Layout.Base.Center.z) : Vector3.zero;
            stage.position = basePos;
            var rng = new System.Random(5);
            for (int i = 0; i < 7; i++)
            {
                var s = new GameObject("Arche" + i).transform;
                s.SetParent(stage, false);
                var mb = new MultiBuilder();
                mb.For(Mats.Get(Mats.Metal, new Color(0.92f, 0.93f, 0.95f))).CylinderZ(Vector3.zero, 3f, 26f, 14);
                mb.For(Mats.Get(Mats.Emissive, new Color(1f, 0.85f, 0.5f), new Color(3f, 2.4f, 1.2f))).Sphere(new Vector3(0, -3f, 0), 1.2f, 8, 6);
                for (int k = 0; k < 8; k++) mb.For(Mats.Get(Mats.Emissive, new Color(0.6f, 0.9f, 1f), new Color(1.5f, 2.2f, 3f))).Box(new Vector3(0, 3.02f, -11 + k * 3), new Vector3(1.2f, 0.05f, 0.8f));
                mb.Build("Hull", s);
                s.localPosition = new Vector3((float)(rng.NextDouble() * 160 - 80), 180f + (float)rng.NextDouble() * 80, 60f + (float)rng.NextDouble() * 120);
                s.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                ships.Add(s);
            }
        }

        void Finish()
        {
            if (!playing) return;
            playing = false;
            AudioManager.StopEnding();
            if (CameraRig.I != null) CameraRig.I.Cinematic = false;
            if (Atmosphere.I != null) Atmosphere.I.ForcePhase = -1f;
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
            var d = done; done = null;
            d?.Invoke();
        }

        void Update()
        {
            if (!playing) return;
            t += Time.unscaledDeltaTime;
            bool hold = Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            skipHold = hold ? skipHold + Time.unscaledDeltaTime : 0f;
            if (skipHold > 1f || t > Length) { Finish(); return; }
            if (Atmosphere.I != null) Atmosphere.I.ForcePhase = Mathf.Lerp(0.7f, 0.78f, t / Length);
            // Archen sinken herab
            for (int i = 0; i < ships.Count; i++)
            {
                var s = ships[i];
                var p = s.localPosition;
                p.y = Mathf.Max(40f + i * 6f, p.y - Time.unscaledDeltaTime * (6f + i));
                s.localPosition = p;
            }
            var cam = Camera.main;
            if (cam != null && stage != null)
            {
                float a = t * 0.05f + 3.6f;
                var center = stage.position + new Vector3(0, 12, 20);
                cam.transform.position = center + new Vector3(Mathf.Cos(a) * 45f, 14f + Mathf.Sin(t * 0.1f) * 4f, Mathf.Sin(a) * 45f);
                cam.transform.rotation = Quaternion.LookRotation(center + Vector3.up * (10 + t * 1.2f) - cam.transform.position);
                cam.fieldOfView = 55f;
            }
        }

        void OnGUI()
        {
            if (!playing) return;
            GUI.depth = -500;
            var w = GameApp.I != null ? GameApp.I.W : null;
            float scale = Screen.height / 1080f * (GameApp.I != null ? GameApp.I.Settings.TextScale : 1f);
            float bar = Screen.height * 0.08f;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
            var big = new GUIStyle(GUI.skin.label) { fontSize = (int)(54 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            var mid = new GUIStyle(big) { fontSize = (int)(32 * scale), fontStyle = FontStyle.Normal };
            var small = new GUIStyle(mid) { fontSize = (int)(24 * scale) };
            Func<float, float, float> fade = (a, b) => Mathf.Clamp01(Mathf.Min((t - a) / 1.5f, (b - t) / 1.5f));

            if (t < 12f)
            {
                GUI.color = new Color(1f, 0.9f, 0.7f, fade(0.5f, 12f));
                GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 90 * scale), "PROGRAMM ZWEITE CHANCE – BEDINGUNG ERFÜLLT", mid);
                GUI.Label(new Rect(0, Screen.height * 0.26f, Screen.width, 90 * scale), "Die Arche HORIZONT kehrt zurück.", big);
            }
            else if (t < 36f && w != null)
            {
                // Rückblick auf die vier Welten (echte Werte aus dem Spielstand)
                int i = Mathf.Clamp((int)((t - 12f) / 6f), 0, GameData.PlanetOrder.Count - 1);
                string pl = GameData.PlanetOrder[i];
                var pd = GameData.Planets[pl];
                float rest = w.Planets.ContainsKey(pl) ? Rules.PlanetRestoration(w, w.Planets[pl]) : 0f;
                float a0 = 12f + i * 6f;
                GUI.color = new Color(Mats.C(pd.Accent).r, Mats.C(pd.Accent).g, Mats.C(pd.Accent).b, fade(a0, a0 + 6f));
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 80 * scale), pd.Name + " – " + pd.Subtitle, big);
                GUI.color = new Color(1, 1, 1, fade(a0, a0 + 6f));
                GUI.Label(new Rect(0, Screen.height * 0.29f, Screen.width, 60 * scale), "Wiederhergestellt: " + (rest * 100f).ToString("0") + " %", mid);
                var sb = new System.Text.StringBuilder();
                for (int ar = 0; ar < 3; ar++)
                {
                    var ps = w.Planets.ContainsKey(pl) ? w.Planets[pl] : null;
                    int stage = ps != null ? Rules.AreaStage(w, ps, ar) : 0;
                    sb.Append(pd.AreaNames[ar]).Append(": ").Append(Rules.StageNames[stage]).Append("   ");
                }
                GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.35f, Screen.width * 0.8f, 80 * scale), sb.ToString(), small);
            }
            else if (w != null)
            {
                float a = Mathf.Clamp01((t - 36f) / 1.5f);
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(0, Screen.height * 0.16f, Screen.width, 80 * scale), "Danke, MIKO.", big);
                string stats = "Objekte gesammelt: " + w.Stat("collected") + "   ·   Credits verdient: " + w.Stat("credEarned") + "   ·   Spielzeit: " + (w.PlayTime / 3600.0).ToString("0.0") + " h";
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 60 * scale), stats, small);
                float scroll = (t - 38f) * 40f * scale;
                string[] credits =
                {
                    "RE:PLANET – Eine zweite Chance", "", "Idee und Auftrag: das RE:PLANET-Team", "Spielentwurf, Programmierung, Grafik und Musik: prozedural erzeugt im Projekt",
                    "Inspiriert von WALL·E (Pixar) – eigene Figuren, Welten und Namen", "", "Die Welten gehören jetzt dir. Freies Spiel beginnt …"
                };
                for (int i = 0; i < credits.Length; i++)
                    GUI.Label(new Rect(0, Screen.height * 0.75f - scroll + i * 44 * scale, Screen.width, 44 * scale), credits[i], small);
            }
            var hs = new GUIStyle(GUI.skin.label) { fontSize = (int)(20 * scale), alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1, 1, 1, 0.6f);
            GUI.Label(new Rect(0, Screen.height - bar + 8 * scale, Screen.width - 30, 30 * scale), "Gedrückt halten zum Überspringen", hs);
            GUI.color = Color.white;
        }
    }
}
