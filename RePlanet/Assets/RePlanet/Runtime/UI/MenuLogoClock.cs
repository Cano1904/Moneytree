namespace RePlanet
{
    /// <summary>
    /// Uhr der Logo-Einflug-Animation im Hauptmenü (reines C#, von der Prüfumgebung mitgeprüft).
    /// Früher wurde die Animation neu gestartet, sobald zwischen zwei Menü-Bildern mehr als 0,5 s lagen – ein einzelnes
    /// langsames Bild (Musik-Clips anlegen, Spielstand lesen, Speicherbereinigung, Menü-Hintergrund aufbauen) ließ dadurch
    /// alle Buchstaben kurz verschwinden und neu einfliegen (sichtbares Flackern). Jetzt startet sie nur über
    /// <see cref="Restart"/> (Menü wirklich neu geöffnet), und ein langes Bild zählt höchstens <see cref="MaxStep"/> Sekunden.
    /// </summary>
    public sealed class MenuLogoClock
    {
        /// <summary>Höchstens so viel Animationszeit pro Bild (ein Ruckler springt nicht vor und startet nichts neu).</summary>
        public const float MaxStep = 0.1f;
        public const float LetterDelay = 0.07f, LetterDur = 0.45f, StartDelay = 0.2f;

        float t;
        int lastFrame = int.MinValue;
        bool pending = true;

        /// <summary>Anzahl der (Neu-)Starts der Einflug-Animation (Leistungsanzeige/Prüfung).</summary>
        public int Starts { get; private set; }
        public float T { get { return t; } }

        /// <summary>Beim nächsten Bild von vorn beginnen (Menü geöffnet).</summary>
        public void Restart() { pending = true; }

        /// <summary>Einmal pro Bild weiterzählen (mehrere OnGUI-Ereignisse im selben Bild zählen einmal).</summary>
        public float Tick(int frame, float dt)
        {
            if (pending)
            {
                pending = false; t = 0f; lastFrame = frame; Starts++;
                return t;
            }
            if (frame != lastFrame)
            {
                lastFrame = frame;
                if (dt > 0f) t += dt < MaxStep ? dt : MaxStep;
            }
            return t;
        }

        /// <summary>Deckkraft (weich, 0–1) von Buchstabe i zur Zeit t.</summary>
        public static float LetterAlpha(float t, int i)
        {
            float a = (t - StartDelay - i * LetterDelay) / LetterDur;
            a = a < 0f ? 0f : a > 1f ? 1f : a;
            return a * a * (3f - 2f * a);
        }

        /// <summary>Fortschritt des gesamten Einflugs (0–1) für n Buchstaben.</summary>
        public static float Reveal(float t, int n)
        {
            float a = (t - StartDelay) / (LetterDur + n * LetterDelay);
            return a < 0f ? 0f : a > 1f ? 1f : a;
        }
    }
}
