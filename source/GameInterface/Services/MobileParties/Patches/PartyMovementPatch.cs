using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal static class PartyMovementPatch
{
    private static MobileParty AllowedChangeParty;

    [HarmonyPrefix]
    [HarmonyPatch("TargetPosition")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool TargetPositionPrefix(ref MobileParty __instance, ref Vec2 value)
    {
        if (AllowedChangeParty == __instance) 
        {
            return true;
        }

        var message = new PartyTargetPositionAttempted(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    internal static readonly PropertyInfo MobileParty_TargetPosition = typeof(MobileParty).GetProperty(nameof(MobileParty.TargetPosition));
    public static void SetTargetPositionOriginal(MobileParty party, ref Vec2 position)
    {
        AllowedChangeParty = party;
        lock (AllowedChangeParty)
        {
            MobileParty_TargetPosition.SetValue(party, position);
        }
        AllowedChangeParty = null;
    }
}
