using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// TNT werfen: Taste halten (Tastatur Q, Controller B) = zielen und Wurfkraft aufladen (Kamera bestimmt Richtung und Höhe,
    /// Vorschau der Flugbahn als gepunktete Linie mit Landemarke, siehe <see cref="TntView"/>), loslassen = werfen,
    /// Interagieren bricht ab. Außerdem: harmloser Flug nach einem Treffer (Bogen zur vom Server bestimmten Stelle) und
    /// kurze Benommenheit (keine Steuerung).
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>Gerade beim Zielen (Vorschau sichtbar).</summary>
        public bool TntAiming { get; private set; }
        /// <summary>Wurfkraft 0…1 beim Zielen.</summary>
        public float TntCharge { get; private set; }
        /// <summary>Vorausberechnete Flugbahn (gleiche Rechnung wie der Server) und ihr Ergebnis.</summary>
        public readonly List<V3> TntPreview = new List<V3>();
        public TntFlight TntPreviewFlight;

        float tntPreviewTimer;
        Vector3 knockFrom, knockTo;
        float knockT = -1f, knockDur = 1f, stunLeft;

        bool TntFrozen { get { return knockT >= 0f || stunLeft > 0f; } }
        /// <summary>Restliche Benommenheit (s) – für Darstellung und HUD.</summary>
        public float StunLeft { get { return knockT >= 0f ? stunLeft + (knockDur - knockT) : stunLeft; } }

        /// <summary>Getroffen: in einem Bogen zur Zielstelle fliegen, danach benommen.</summary>
        public void Knockback(Vector3 from, Vector3 to, float air, float stun)
        {
            knockFrom = RenderPos.sqrMagnitude > 0.01f ? RenderPos : from;
            knockTo = to;
            knockDur = Mathf.Max(0.2f, air);
            knockT = 0f;
            stunLeft = stun;
            TntAiming = false;
            TntPreview.Clear();
        }

        static float AimElevation()
        {
            float pitch = CameraRig.I != null ? CameraRig.I.Pitch : 18f;
            return Mathf.Clamp(0.62f + (18f - pitch) * Mathf.Deg2Rad * 1.1f, 0.12f, 1.2f);
        }

        void StepTnt(WorldState w, PlayerData me, bool noInput, float dt)
        {
            // Flug nach einem Treffer
            if (knockT >= 0f)
            {
                knockT += dt;
                float k = Mathf.Clamp01(knockT / knockDur);
                var p = Vector3.Lerp(knockFrom, knockTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.4f;
                ms.Pos = C(p); ms.Vel = V3.Zero; ms.Speed = 0f;
                if (k >= 1f) { knockT = -1f; ms.Pos = C(knockTo); }
            }
            else if (stunLeft > 0f) { stunLeft -= dt; ms.Vel = V3.Zero; ms.Speed = 0f; }

            if (noInput || me == null)
            {
                if (TntAiming) { TntAiming = false; TntPreview.Clear(); }
                return;
            }
            if (!TntAiming && InputMap.Down(GameAction.ThrowTnt))
            {
                if (ms.Swimming || ms.Diving) return; // Controller: B taucht im Wasser ab
                var probe = new PlayerData { Id = me.Id, Pos = ms.Pos, Tnt = me.Tnt, Vehicle = me.Vehicle, TowTimer = me.TowTimer };
                var why = Rules.TntThrowCheck(w, w.Cur, probe);
                if (why != null) { Hud.Show(Loc.T(why), ToastKind.Info, 3f); AudioManager.Ui("beep_error"); return; }
                TntAiming = true; TntCharge = 0.15f; tntPreviewTimer = 0f;
            }
            if (!TntAiming) return;
            if (InputMap.Down(GameAction.Interact)) { TntAiming = false; TntPreview.Clear(); Hud.Show(Loc.T("Wurf abgebrochen."), ToastKind.Info, 1.5f); return; }
            float yaw = (CameraRig.I != null ? CameraRig.I.Yaw : ms.Yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            var dir = Rules.TntDirection(yaw, AimElevation());
            ms.Yaw = yaw;
            if (InputMap.Held(GameAction.ThrowTnt))
            {
                TntCharge = Mathf.Min(1f, TntCharge + dt / 1.1f);
                tntPreviewTimer -= dt;
                if (tntPreviewTimer <= 0f)
                {
                    tntPreviewTimer = 0.05f;
                    var vel = Rules.TntVelocity(dir, TntCharge);
                    TntPreviewFlight = Rules.TntSimulate(w.Cur, Rules.TntStart(ms.Pos, vel), vel, TntPreview);
                }
                Hud.Progress = TntCharge;
                Hud.ProgressLabel = Loc.F("Wurfkraft · TNT ×{0}", me.Tnt);
                return;
            }
            // losgelassen: werfen
            TntAiming = false;
            TntPreview.Clear();
            float charge = TntCharge;
            Act(new JObj().Set("a", "tnt").Set("dir", Json.Arr(Json.R(dir.x, 4), Json.R(dir.y, 4), Json.R(dir.z, 4))).Set("s", Json.R(charge, 3)));
            if (ActorsView.I != null && ActorsView.I.LocalRobot != null) ActorsView.I.LocalRobot.Grab();
        }
    }
}
