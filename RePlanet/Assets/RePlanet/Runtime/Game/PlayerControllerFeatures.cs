using RePlanet.Core;
using UnityEngine;

namespace RePlanet
{
    /// <summary>Interaktion mit Helferrobotern: defekte reparieren (halten), reparierte mitnehmen bzw. an neuem Ort absetzen.</summary>
    public partial class PlayerController
    {
        Interaction BotInteraction(WorldState w, PlayerData me)
        {
            var l = WorldGen.Get(w.CurrentPlanet);
            string ek = InputMap.Label(GameAction.Interact);
            // Folgt mir ein Helfer? Dann jederzeit absetzen
            foreach (var b in w.Cur.Bots.Values)
                if (b.Follow == me.Id)
                {
                    string why = Rules.HelperWorkplaceCheck(w.Cur, ms.Pos);
                    return why == null
                        ? new Interaction { Kind = "botstay", Id = b.Id, Label = "[" + ek + "] Helfer hier arbeiten lassen (Umkreis " + GameData.HelperRadius.ToString("0") + " m)", Key = ek, Word = "Helfer absetzen", At = U(b.Pos) + Vector3.up * 1.6f }
                        : new Interaction { Kind = "none", Label = "Helfer folgt dir – " + why, Word = "Helfer folgt" };
                }
            foreach (var s in l.Bots)
            {
                HelperBot bot;
                bool fixedBot = w.Cur.Bots.TryGetValue(s.Id, out bot);
                var pos = fixedBot ? bot.Pos : s.Pos;
                if (V3.DistXZ(ms.Pos, pos) > (fixedBot ? 4f : 4.5f)) continue;
                if (fixedBot)
                    return new Interaction { Kind = "botfollow", Id = s.Id, Label = "[" + ek + "] Helfer mitnehmen (neuen Arbeitsort wählen) · sammelt " + bot.Load.Count + "/" + GameData.HelperLoad, Key = ek, Word = "Helfer mitnehmen", At = U(pos) + Vector3.up * 1.6f };
                var why2 = Rules.HelperRepairCheck(w, w.Cur, s.Id);
                if (why2 != null) return new Interaction { Kind = "none", Label = s.Name + ": " + why2 + " (" + Rules.HelperCostText(w.CurrentPlanet) + ")", Word = "Helfer: Material fehlt", At = U(pos) + Vector3.up * 1.6f };
                return new Interaction { Kind = "botfix", Id = s.Id, Hold = true, Label = "[" + ek + " halten] " + s.Name + " reparieren (" + Rules.HelperCostText(w.CurrentPlanet) + ")", Key = ek, Word = "Helfer reparieren", At = U(pos) + Vector3.up * 1.6f };
            }
            return null;
        }
    }
}
