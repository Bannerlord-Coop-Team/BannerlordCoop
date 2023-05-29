using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class PartyMovementPatch
{
    private static MobileParty AllowedChangeParty;

    [HarmonyPrefix]
    [HarmonyPatch("TargetSettlement", MethodType.Setter)]
    private static bool TargetSettlementPrefix(ref MobileParty __instance, ref Settlement value)
    {
        // TODO allow for controlled parties and add synchronisation
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TargetParty", MethodType.Setter)]
    private static bool TargetPartyPrefix(ref MobileParty __instance, ref MobileParty value)
    {
        // TODO allow for controlled parties and add synchronisation
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TargetPosition")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool TargetPositionPrefix(ref MobileParty __instance, ref Vec2 value)
    {
        if (AllowedChangeParty == __instance)
        {
            return true;
        }

        var message = new PartyTargetPositionChanged(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }


    internal static readonly PropertyInfo MobileParty_TargetPosition = typeof(MobileParty).GetProperty(nameof(MobileParty.TargetPosition));
    public static void SetTargetPositionOverride(MobileParty party, ref Vec2 position)
    {
        AllowedChangeParty = party;
        lock (AllowedChangeParty)
        {
            MobileParty_TargetPosition.SetValue(party, position);
        }
        AllowedChangeParty = null;
    }
}
