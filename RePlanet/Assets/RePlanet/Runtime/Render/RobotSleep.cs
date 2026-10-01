using System.Collections.Generic;
using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>
    /// MIKOs Schlaf: Nach dem Schlafbefehl fährt MIKO auf den Ladeplatz (Darstellung, <see cref="SleepSpots"/>), klappt Arm
    /// und Antenne ein, senkt die Karosserie, das Augen-Display zeigt geschlossene Augen und ein kleines „z“, der Körper hebt
    /// und senkt sich langsam wie beim Atmen, ein Ladekabel steckt hinten, die Ladeleuchte pulsiert und kleine „Z“ steigen auf.
    /// Beim Aufwachen: Strecken (Körper hoch, Arm nach oben, Kopf in den Nacken), Augen öffnen sich, kurzer froher Piepser.
    /// Alles folgt dem replizierten <c>PlayerData.Sleeping</c> – Mitspieler sehen dieselbe Animation.
    /// </summary>
    public partial class RobotModel
    {
        /// <summary>Fortschritt der Fahrt zum Ladeplatz 0..1 (setzt ActorsView); erst bei Ankunft beginnt die Schlafhaltung.</summary>
        public float SleepDrive = 1f;
        /// <summary>Aufwach-Piepser abspielen (Spielerfiguren und Intro; Helferroboter leise).</summary>
        public float WakeBeepVolume = 0.5f;
        /// <summary>Schlafhaltung 0 (wach) … 1 (eingeschlafen) – Prüfumgebung/Anzeige.</summary>
        public float SleepAmount { get { return sleepK; } }
        /// <summary>Läuft gerade das Strecken nach dem Aufwachen?</summary>
        public bool Stretching { get { return wakeT > 0f; } }

        float sleepK, wakeT, zClock;
        bool wasAsleep;
        Transform antenna, sleepEyes, visorZ, sleepCable, chargeLed;
        readonly Transform[] floatZ = new Transform[3];
        Material sleepEyeMat, zMat, chargeMat;

        /// <summary>Teile für Schlaf und Aufwachen (am Ende von Build).</summary>
        void BuildSleepParts()
        {
            sleepEyeMat = Mats.Unique(Mats.Emissive, new Color(0.45f, 0.6f, 1f));
            Mats.SetEmission(sleepEyeMat, new Color(0.3f, 0.45f, 1f) * 0.8f);
            zMat = Mats.Unique(Mats.Emissive, new Color(0.7f, 0.85f, 1f));
            Mats.SetEmission(zMat, new Color(0.5f, 0.75f, 1.4f));
            chargeMat = Mats.Unique(Mats.Emissive, new Color(0.35f, 1f, 0.75f));
            Mats.SetEmission(chargeMat, Color.black);
            // Geschlossene Augen „‿ ‿“ als Bögen aus kleinen Segmenten auf dem Visier
            sleepEyes = Node(head, "sleepEyes", Vector3.zero);
            var arc = MeshKit.Get("mikoSleepEye", b =>
            {
                const int n = 7;
                for (int k = 0; k < n; k++)
                {
                    float a0 = Mathf.Lerp(205f, 335f, k / (float)n) * Mathf.Deg2Rad, a1 = Mathf.Lerp(205f, 335f, (k + 1) / (float)n) * Mathf.Deg2Rad;
                    var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 0.75f, 0) * 0.07f;
                    var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 0.75f, 0) * 0.07f;
                    var mid = (p0 + p1) * 0.5f;
                    float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                    b.BoxRot(mid, new Vector3((p1 - p0).magnitude * 1.15f, 0.016f, 0.006f), new Vector3(0, 0, ang));
                }
            });
            foreach (var sx in new[] { -0.17f, 0.17f })
                Part(sleepEyes, arc, sleepEyeMat, new Vector3(sx, 0.03f, 0.238f), Vector3.one, Vector3.zero, "closedEye", false);
            var zMesh = MeshKit.Get("mikoZ", b =>
            {
                b.Box(new Vector3(0, 0.5f, 0), new Vector3(0.9f, 0.16f, 0.12f));
                b.Box(new Vector3(0, -0.5f, 0), new Vector3(0.9f, 0.16f, 0.12f));
                b.BoxRot(Vector3.zero, new Vector3(1.22f, 0.16f, 0.12f), new Vector3(0, 0, 48f));
            });
            visorZ = Part(sleepEyes, zMesh, sleepEyeMat, new Vector3(0.0f, -0.055f, 0.238f), Vector3.one * 0.045f, Vector3.zero, "visorZ", false);
            sleepEyes.gameObject.SetActive(false);
            // aufsteigende „Z“ über dem Kopf (im Roboterraum, folgen der Karosserie nicht)
            for (int i = 0; i < floatZ.Length; i++)
            {
                floatZ[i] = Part(root, zMesh, zMat, new Vector3(0.1f, 1.3f, 0.1f), Vector3.one * 0.1f, Vector3.zero, "floatZ", false);
                floatZ[i].gameObject.SetActive(false);
            }
            // Ladekabel hinten (eingesteckt) und Ladeleuchte an der Karosserie
            sleepCable = Group(root, "chargeCable", Vector3.zero, float.NaN, mb =>
            {
                var pts = new[] { new Vector3(0.18f, 0.5f, -0.46f), new Vector3(0.22f, 0.36f, -0.6f), new Vector3(0.3f, 0.06f, -0.8f), new Vector3(0.55f, 0.03f, -1.15f), new Vector3(0.9f, 0.03f, -1.35f) };
                for (int k = 0; k < pts.Length - 1; k++) P(mb, SRubber).Tube(pts[k], pts[k + 1], 0.022f, 6);
                P(mb, SAccent).BevelBox(new Vector3(0.18f, 0.5f, -0.45f), new Vector3(0.07f, 0.07f, 0.06f), 0.012f); // Stecker
                P(mb, SDark).BevelBox(new Vector3(0.95f, 0.05f, -1.38f), new Vector3(0.16f, 0.1f, 0.12f), 0.02f);    // Bodendose
            }, false);
            sleepCable.gameObject.SetActive(false);
            chargeLed = Part(body, MeshKit.Cube, chargeMat, new Vector3(0.15f, 0.12f, -0.459f), new Vector3(0.12f, 0.03f, 0.012f), Vector3.zero, "chargeLed", false);
        }

        /// <summary>Schlafhaltung und Aufwachen (nach der Grundanimation und der Mimik).</summary>
        void ApplySleep(float dt, bool sleeping)
        {
            if (sleepEyes == null) return;
            bool asleep = sleeping && SleepDrive >= 0.97f;
            // Aufwachen: einmal strecken, piepsen, freuen
            if (wasAsleep && !sleeping && sleepK > 0.4f)
            {
                wakeT = 1.8f;
                Emote("happy");
                if (WakeBeepVolume > 0f) AudioManager.Play("beep_happy", transform.position + Vector3.up, WakeBeepVolume, 1.12f);
            }
            wasAsleep = sleeping && sleepK > 0.4f;
            float k = sleepK;
            float t = Time.time;
            if (k > 0.001f)
            {
                // Arm ganz eingeklappt, Kopf gesenkt, Antenne nach hinten gelegt, langsames Atmen
                arm1.localRotation = Quaternion.Slerp(arm1.localRotation, Quaternion.Euler(-84f, -4f, 0f), k);
                arm2.localRotation = Quaternion.Slerp(arm2.localRotation, Quaternion.Euler(168f, 0f, 0f), k);
                float breath = Mathf.Sin(t * 1.25f);
                body.localPosition += new Vector3(0f, -0.05f * k + breath * 0.011f * k, 0f);
                body.localRotation = body.localRotation * Quaternion.Euler(breath * 0.8f * k, 0f, 0f);
                head.localRotation = head.localRotation * Quaternion.Euler(13f * k + breath * 1.2f * k, 0f, 0f);
            }
            if (antenna != null)
            {
                float fold = Mathf.Max(k, 0f);
                antenna.localRotation = Quaternion.Euler(-82f * fold, 0f, 0f);
                antenna.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.7f, fold), 1f);
            }
            // Augen-Display: geschlossene Augen statt Leuchtaugen, gedimmt und langsam pulsierend; „z“ blinkt
            bool closed = k > 0.45f;
            if (sleepEyes.gameObject.activeSelf != closed) sleepEyes.gameObject.SetActive(closed);
            if (eyeL.gameObject.activeSelf == closed) { eyeL.gameObject.SetActive(!closed); eyeR.gameObject.SetActive(!closed); }
            if (closed)
            {
                for (int i = 0; i < 2; i++) { lidUp[i].gameObject.SetActive(false); lidDown[i].gameObject.SetActive(false); }
                float pulse = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 1.25f));
                Mats.SetEmission(sleepEyeMat, new Color(0.3f, 0.45f, 1f) * pulse * k);
                visorZ.gameObject.SetActive(Mathf.Repeat(t, 2.4f) < 1.5f);
            }
            // Laden: Kabel steckt, Ladeleuchte pulsiert (grün-türkis, wie ein ruhiger Herzschlag)
            bool charging = k > 0.3f;
            if (sleepCable.gameObject.activeSelf != charging) sleepCable.gameObject.SetActive(charging);
            float led = charging ? (0.4f + 1.6f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 2.2f), 3f)) * k : 0f;
            Mats.SetEmission(chargeMat, new Color(0.35f, 1f, 0.75f) * led);
            // aufsteigende Z: versetzt, wachsen beim Steigen, verschwinden oben
            zClock += dt;
            for (int i = 0; i < floatZ.Length; i++)
            {
                bool on = k > 0.85f;
                if (!on) { if (floatZ[i].gameObject.activeSelf) floatZ[i].gameObject.SetActive(false); continue; }
                float u = Mathf.Repeat(zClock / 3.3f + i / (float)floatZ.Length, 1f);
                float s = Mathf.Sin(u * Mathf.PI) * (0.07f + u * 0.07f);
                if (!floatZ[i].gameObject.activeSelf) floatZ[i].gameObject.SetActive(true);
                floatZ[i].localPosition = new Vector3(0.12f + Mathf.Sin(u * 5f + i) * 0.1f + u * 0.18f, 1.18f + u * 0.75f, 0.05f);
                floatZ[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * 4f + i) * 12f);
                floatZ[i].localScale = Vector3.one * Mathf.Max(0.001f, s);
            }
            if (k > 0.85f) Mats.SetEmission(zMat, new Color(0.5f, 0.75f, 1.4f) * (0.6f + 0.4f * Mathf.Sin(t * 1.25f)));
            // Strecken nach dem Aufwachen
            if (wakeT > 0f)
            {
                wakeT -= dt;
                float u = 1f - Mathf.Clamp01(wakeT / 1.8f);
                float env = Mathf.Sin(Mathf.Clamp01(u * 1.25f) * Mathf.PI);
                body.localPosition += new Vector3(0f, 0.07f * env, 0f);
                body.localRotation = body.localRotation * Quaternion.Euler(-7f * env, 0f, Mathf.Sin(u * 9f) * 2f * env);
                head.localRotation = head.localRotation * Quaternion.Euler(-15f * env, Mathf.Sin(u * 6f) * 10f * env, 0f);
                arm1.localRotation = Quaternion.Slerp(arm1.localRotation, Quaternion.Euler(-128f, 12f, 0f), env);
                arm2.localRotation = Quaternion.Slerp(arm2.localRotation, Quaternion.Euler(8f, 0f, 0f), env);
                float wide = 1f + 0.25f * env;
                eyeL.localScale = new Vector3(eyeL.localScale.x * wide, eyeL.localScale.y * wide, eyeL.localScale.z);
                eyeR.localScale = new Vector3(eyeR.localScale.x * wide, eyeR.localScale.y * wide, eyeR.localScale.z);
            }
        }
    }

    /// <summary>
    /// Ladeplatz zum Schlafen (nur Darstellung, aus Layout und Spielerreihenfolge – auf allen Rechnern gleich): im Hangar
    /// der Ladering, im Laderaum die Mitte, am Stützpunkt die Ladefläche; weitere Spieler nebeneinander. Im Gelände
    /// (Unterschlupf) bleibt MIKO, wo er ist.
    /// </summary>
    public static class SleepSpots
    {
        public static bool Find(PlanetLayout l, WorldState w, PlayerData p, out Vector3 spot, out float yawRad)
        {
            spot = Vector3.zero; yawRad = 0f;
            if (l == null || w == null || p == null) return false;
            int idx = 0;
            var ids = new List<string>();
            foreach (var o in w.Players.Values) if (o.Online) ids.Add(o.Id);
            ids.Sort(System.StringComparer.Ordinal);
            idx = Mathf.Max(0, ids.IndexOf(p.Id));
            var room = Rules.RoomAt(l, p.Pos);
            if (room != null)
            {
                var r = room.Inner;
                if (room.Kind == Rules.ShelterHangar)
                {
                    // Ladering vorn rechts vor der Ladesäule (WorldViewBase.HangarInterior), weitere Plätze nach links
                    float x = r.Cx + r.Hx - 2.0f - idx * 2.2f, z = r.Cz - 1.5f;
                    x = Mathf.Max(x, r.Cx - r.Hx + 1.4f);
                    spot = new Vector3(x, r.Y0, z);
                    yawRad = Mathf.Atan2(room.OutX, room.OutZ);
                    return true;
                }
                // Laderaum: entlang der Längsachse, Blick zur Rampe
                var dir = new Vector3(-room.OutX, 0f, -room.OutZ);
                var c = new Vector3(room.Spot.x, room.Spot.y, room.Spot.z) + dir * ((idx % 2 == 0 ? 1f : -1f) * 1.4f * ((idx + 1) / 2));
                spot = c;
                yawRad = Mathf.Atan2(room.OutX, room.OutZ);
                return true;
            }
            V3 ch;
            if (l.Base.Stations.TryGetValue("charge", out ch) && V3.DistXZ(p.Pos, ch) < 12f && l.Base.InBase(p.Pos.x, p.Pos.z))
            {
                float off = (idx % 2 == 0 ? 1f : -1f) * 1.6f * ((idx + 1) / 2);
                spot = new Vector3(ch.x + off, l.GroundAt(ch.x + off, ch.z), ch.z);
                yawRad = 0f;
                return true;
            }
            return false;
        }
    }
}
