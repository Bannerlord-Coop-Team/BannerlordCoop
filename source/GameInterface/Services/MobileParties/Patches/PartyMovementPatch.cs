using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch]
internal class PartyMovementPatch
{
    private static MobilePartyAi AllowedChangePartyAi;

    #region MobileParty
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobileParty), "TargetSettlement", MethodType.Setter)]
    private static bool SetTargetSettlementPrefix(ref MobileParty __instance, ref Settlement value)
    {
        return AllowedChangePartyAi == __instance.Ai;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobileParty), "TargetParty", MethodType.Setter)]
    private static bool SetTargetPartyPrefix(ref MobileParty __instance, ref MobileParty value)
    {
        return AllowedChangePartyAi == __instance.Ai;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobileParty), "TargetPosition", MethodType.Setter)]
    private static bool SetTargetPositionPrefix(ref MobileParty __instance, ref Vec2 value)
    {
        return AllowedChangePartyAi == __instance.Ai;
    }
    #endregion
    #region MobilePartyAi
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobilePartyAi), "DefaultBehavior", MethodType.Setter)]
    private static bool SetDefaultBehaviorPrefix(ref MobilePartyAi __instance, ref AiBehavior value)
    {
        if (AllowedChangePartyAi != __instance)
            value = AiBehavior.None;

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobilePartyAi), "SetMoveGoToSettlement")]
    private static bool SetMoveGoToSettlementPrefix(ref MobilePartyAi __instance, ref Settlement settlement)
    {
        // TODO allow for controlled parties and add synchronisation
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobilePartyAi), "SetMoveEngageParty")]
    private static bool SetMoveEngagePartyPrefix(ref MobilePartyAi __instance, ref MobileParty party)
    {
        // TODO allow for controlled parties and add synchronisation
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MobilePartyAi), "SetMoveGoToPoint")]
    private static bool SetMoveGoToPointPrefix(ref MobilePartyAi __instance, ref Vec2 point)
    {
        if (AllowedChangePartyAi == __instance)
        {
            return true;
        }

        var message = new PartyTargetPositionChanged(__instance.GetMobileParty(), point);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
    #endregion

    public static void SetMoveGoToPointOverride(MobileParty party, ref Vec2 position)
    {
        AllowedChangePartyAi = party.Ai;
        lock (AllowedChangePartyAi)
        {
            AllowedChangePartyAi.SetMoveGoToPoint(position);
        }
        AllowedChangePartyAi = null;
    }
}
