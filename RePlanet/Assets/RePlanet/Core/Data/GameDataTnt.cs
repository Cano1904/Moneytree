using System;

namespace RePlanet.Core
{
    /// <summary>
    /// TNT: Balancing-Werte für Sprengladungen (kaufen, werfen, Zündschnur, Sprengradius, Müllberge in Stücke sprengen).
    /// Regeln: <see cref="Rules"/> (TntRules.cs), Ablauf auf dem Server: <see cref="Game"/> (GameTnt.cs).
    /// Wirkung mit dem Kampagnen-Bot messen: <c>cd Tests && dotnet run -c Release -- balance</c>.
    /// </summary>
    public static partial class GameData
    {
        /// <summary>Höchstens so viele Ladungen trägt ein Roboter bei sich.</summary>
        public const int TntMaxCarry = 3;
        /// <summary>Zündschnur ab dem Wurf (s); landet die Ladung später, brennt sie danach noch <see cref="TntMinLyingFuse"/> s.</summary>
        public const float TntFuse = 4f, TntMinLyingFuse = 0.8f;
        /// <summary>Wartezeit zwischen zwei Würfen desselben Spielers (s).</summary>
        public const float TntThrowCooldown = 1.2f;
        /// <summary>Abwurfgeschwindigkeit bei geringster bzw. voller Wurfkraft (m/s) und Abwurfhöhe über dem Boden (m).</summary>
        public const float TntSpeedMin = 4.5f, TntSpeedMax = 16.5f, TntThrowHeight = 1.3f;
        /// <summary>Schwerkraft der Flugbahn (m/s²) und Zeitschritt der Bahnberechnung (s) – Server und Vorschau rechnen gleich.</summary>
        public const float TntGravity = 9.81f, TntStep = 1f / 30f, TntMaxFlight = 5f;
        /// <summary>Sprengradius für Roboter: wer näher steht, fliegt harmlos durch die Luft und ist kurz benommen.</summary>
        public const float TntKnockRadius = 6.5f;
        /// <summary>Flugweite der Getroffenen (m): nah an der Ladung weit, am Rand kurz (bewusst unter 6 m – kein Teleport beim Client).</summary>
        public const float TntKnockMin = 2.2f, TntKnockMax = 5.4f;
        /// <summary>Flugzeit der Getroffenen (s) und Benommenheit danach (s, keine Werkzeuge, keine Steuerung).</summary>
        public const float TntKnockAir = 0.9f, TntStun = 1.6f;
        /// <summary>Wie lange der Ruß auf MIKOs Augen-Display bleibt (s, reine Darstellung).</summary>
        public const float TntSootTime = 6f;
        /// <summary>Eine Ladung sprengt einen Müllberg, wenn sie höchstens so weit von seinem Rand entfernt explodiert (m).</summary>
        public const float TntHeapReach = 3f;
        /// <summary>Nach einer Sprengung muss sich der Staub legen, bevor derselbe Müllberg wieder gesprengt werden kann (s).</summary>
        public const float TntHeapCooldown = 12f;
        /// <summary>Stücke je Sprengung (vor dem Staubverlust); große Müllberge (Radius ab 7 m) +2.</summary>
        public const int TntPiecesPerStage = 14;
        /// <summary>Anteil der Stücke, der als Staub verloren geht.</summary>
        public const float TntDustShare = 0.15f;
        /// <summary>
        /// Stücke aus gesprengten Müllbergen zählen beim Einsammeln zum Hauptmüll ihres Bereichs – höchstens bis zu diesem
        /// Anteil des Bereichs. Danach bringen Sprengungen nur noch Material.
        /// </summary>
        public const float TntCleanShare = 0.25f;

        /// <summary>Preis einer Ladung in der Werkstatt (je Planet ≈ Wert einer Sprengung an unsortiertem Material).</summary>
        public static int TntPrice(string planet)
        {
            switch (planet)
            {
                case "pyra": return 35;
                case "pelagia": return 30;
                case "nivalis": return 45;
                default: return 25;
            }
        }

        /// <summary>Sprengungen, bis ein Müllberg ganz zerlegt ist (große Berge brauchen drei Ladungen).</summary>
        public static int MoundStages(Mound m) { return m != null && m.Radius > 6.5f ? 3 : 2; }
    }
}
