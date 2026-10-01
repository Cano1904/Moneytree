using System;

namespace RePlanet.Core
{
    /// <summary>Englisch: MIKOs Feldtablet (Spielmenü) und Flacker-Diagnose der Leistungsanzeige.</summary>
    public static partial class Loc
    {
        static void FillEnglishTablet(Action<string, string> e)
        {
            e("MIKO · Feldtablet", "MIKO · Field tablet"); e("Solo", "Solo"); e("Aktuelles Ziel", "Current objective");
            e("Schriftatlas neu: {0} · Logo-Starts: {1}", "Font atlas rebuilds: {0} · logo starts: {1}");
        }
    }
}
