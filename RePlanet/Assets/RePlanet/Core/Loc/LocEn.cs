using System;

namespace RePlanet.Core
{
    /// <summary>Englische Übersetzung (Schlüssel = deutscher Text; Vorlagen mit {0} …).</summary>
    public static partial class Loc
    {
        static void FillEnglish(Action<string, string> e)
        {
            e("Fortsetzen", "Continue"); e("Neues Spiel", "New Game"); e("Koop", "Co-op"); e("Spielstände", "Saves");
            e("Einstellungen", "Settings"); e("Beenden", "Quit"); e("Zurück", "Back"); e("Speichern", "Save"); e("Laden", "Load");
        }
    }
}
