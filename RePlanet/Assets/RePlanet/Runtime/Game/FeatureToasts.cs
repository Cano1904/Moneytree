using RePlanet.Core;

namespace RePlanet
{
    /// <summary>Hinweise (Toasts) für Erfolge, Weltereignisse, Helferroboter, Schnellreise und das Abwarten eines Sturms.</summary>
    public static class FeatureToasts
    {
        public static void Show(GameApp app, JObj f)
        {
            if (app == null || f == null) return;
            string name = f.Str("name");
            bool me = app.Client != null && f.Str("pid") == app.Client.Pid;
            switch (f.Str("k"))
            {
                case "achievement":
                    Hud.Show("★ Erfolg: " + name + " – Belohnung: " + (f.Str("rewardName") ?? "") + " (Roboter-Reiter)", ToastKind.Story, 6f);
                    if (app.Profile != null && f.Str("reward") != null && app.Profile.Unlocks.Add(f.Str("reward"))) app.Profile.Save();
                    break;
                case "meteor": Hud.Show("Meteoritenschauer über „" + name + "“! " + f.Int("n") + " wertvolle Splitter sind niedergegangen (Stern-Symbol auf der Karte).", ToastKind.Story, 7f); break;
                case "supply": Hud.Show("Versorgungsabwurf über „" + name + "“ – die Kiste steckt voller Ersatzteile (Rauchzeichen, Karte).", ToastKind.Story, 7f); break;
                case "dump": Hud.Show("Der Sturm hat in „" + name + "“ eine verschüttete Deponie freigelegt (" + f.Int("n") + " Teile).", ToastKind.Info, 6f); break;
                case "botfixed": Hud.Show((me ? "" : "Team: ") + name + " läuft wieder! Er sammelt kleinen Müll im Umkreis und schickt ihn ins Lager.", ToastKind.Success, 6f); break;
                case "botfollow": if (me) Hud.Show("Der Helfer folgt dir. An einer guten Stelle [" + InputMap.Label(GameAction.Interact) + "] – dort arbeitet er weiter.", ToastKind.Info, 5f); break;
                case "botstay": if (me) Hud.Show("Neuer Arbeitsort für den Helfer.", ToastKind.Success, 3f); break;
                case "fasttravel": if (me) Hud.Show("Schnellreise nach „" + name + "“ (−" + f.Float("cost").ToString("0") + " Energie).", ToastKind.Success, 3.5f); break;
                case "wait":
                    if (f.Int("n") < f.Int("of")) Hud.Show("Warte auf Mitspieler (" + f.Int("n") + "/" + f.Int("of") + " warten den Sturm ab) …", ToastKind.Info, 4f);
                    break;
            }
        }
    }
}
