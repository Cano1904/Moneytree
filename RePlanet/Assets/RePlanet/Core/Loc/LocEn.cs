using System;

namespace RePlanet.Core
{
    /// <summary>Englische Übersetzung (Schlüssel = deutscher Text; Vorlagen mit {0} …).</summary>
    public static partial class Loc
    {
        static void FillEnglish(Action<string, string> e)
        {
            FillEnglishUi(e);
            FillEnglishData(e);
            FillEnglishCore(e);
        }
    }
}
