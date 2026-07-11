using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(GainKingdomInfluenceAction))]
internal class GainKingdomInfluenceActionPatch
{
    [HarmonyPatch(nameof(GainKingdomInfluenceAction.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Hero hero, MobileParty party, float gainedInfluence, GainKingdomInfluenceAction.InfluenceGainingReason detail)
    {
        if (ModInformation.IsClient) return;

        int gainedInfluenceInt = MathF.Abs((int)gainedInfluence);
        var clan = GetClan(hero, party);

        // Don't notify players of influence changes that don't involve players
        if (gainedInfluenceInt == 0 
            && (party == null || !party.IsPlayerParty())
            && (hero == null || !hero.IsPlayerHero())) return;

        var message = new NotifyKingdomInfluenceChanged(hero, party, clan, gainedInfluenceInt, detail);
        MessageBroker.Instance.Publish(null, message);
    }

    private static Clan GetClan(Hero hero, MobileParty party)
    {
        Clan clan = null;
        if (hero != null)
        {
            if (hero.CompanionOf != null)
            {
                clan = hero.CompanionOf;
            }
            else if (hero.Clan != null)
            {
                clan = hero.Clan;
            }
        }
        else if (party.ActualClan != null)
        {
            clan = party.ActualClan;
        }
        else if (party.Owner != null)
        {
            clan = party.Owner.Clan;
        }

        return clan;
    }
}
