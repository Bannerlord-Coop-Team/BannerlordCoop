using Common.Extensions;
using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Handles changes in party behavior for the <see cref="MobilePartyAi"/> behavior synchronization system.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[HarmonyPatch(typeof(MobilePartyAi))]
static class PartyBehaviorPatch
{
    static readonly Func<MobilePartyAi, MobileParty> _mobileParty = typeof(MobilePartyAi)
        .GetField("_mobileParty", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedGetter<MobilePartyAi, MobileParty>();

    [HarmonyPrefix]
    [HarmonyPatch("SetAiBehavior")]
    private static bool SetAiBehaviorPrefix(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref PartyBase targetPartyFigure,
        ref Vec2 bestTargetPoint)
    {
        if (BehaviorIsSame(ref __instance, ref newAiBehavior, ref targetPartyFigure, ref bestTargetPoint)) return false;

        MobileParty party = __instance.GetMobileParty();

        bool hasTargetEntity = false;
        string targetEntityId = string.Empty;

        if (targetPartyFigure != null)
        {
            hasTargetEntity = true;
            targetEntityId = targetPartyFigure.IsSettlement
                ? targetPartyFigure.Settlement.StringId
                : targetPartyFigure.MobileParty.StringId;
        }

        var data = new PartyBehaviorUpdateData(party.StringId, newAiBehavior, hasTargetEntity, targetEntityId, bestTargetPoint, party.Position2D);
        var message = new PartyBehaviorChangeAttempted(party, data);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    private static Func<MobilePartyAi, Vec2> get_MobilePartyAi_BehaviorTarget = typeof(MobilePartyAi)
        .GetField("BehaviorTarget", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildUntypedGetter<MobilePartyAi, Vec2>();
    private static bool BehaviorIsSame(
        ref MobilePartyAi __instance,
        ref AiBehavior newAiBehavior,
        ref PartyBase targetPartyFigure,
        ref Vec2 bestTargetPoint)
    {
        MobileParty party = __instance.GetMobileParty();
        IMapEntity targetEntity = null;

        if (targetPartyFigure != null)
        {
            targetEntity = targetPartyFigure.IsSettlement ? targetPartyFigure.MobileParty : targetPartyFigure.Settlement;
        }

        return __instance.AiBehaviorMapEntity == targetEntity && 
            party.ShortTermBehavior == newAiBehavior && 
            get_MobilePartyAi_BehaviorTarget(__instance) == bestTargetPoint;

    }

    public static void SetAiBehavior(
        MobilePartyAi partyAi, AiBehavior newBehavior, IMapEntity targetMapEntity, Vec2 targetPoint)
    {
        DefaultBehavior(partyAi, newBehavior);

        var mobileParty = _mobileParty(partyAi);

        if (typeof(Settlement).IsAssignableFrom(targetMapEntity?.GetType()))
        {
            TargetSettlement(mobileParty, (Settlement)targetMapEntity);
            TargetParty(mobileParty, null);
        }

        else if (typeof(MobileParty).IsAssignableFrom(targetMapEntity?.GetType()))
        {
            TargetSettlement(mobileParty, null);
            TargetParty(mobileParty, (MobileParty)targetMapEntity);
        }

        TargetPosition(mobileParty, targetPoint);

        SetShortTermBehavior(partyAi, newBehavior, targetMapEntity);
        SetBehaviorTarget(partyAi, targetPoint);
        UpdateBehavior(partyAi);
    }

    static readonly Action<MobilePartyAi, AiBehavior, IMapEntity> SetShortTermBehavior = typeof(MobilePartyAi)
        .GetMethod("SetShortTermBehavior", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<MobilePartyAi, AiBehavior, IMapEntity>>();

    static readonly Action<MobilePartyAi, Vec2> SetBehaviorTarget = typeof(MobilePartyAi)
        .GetField("BehaviorTarget", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedSetter<MobilePartyAi, Vec2>();

    static readonly Action<MobilePartyAi> UpdateBehavior = typeof(MobilePartyAi)
        .GetMethod("UpdateBehavior", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildDelegate<Action<MobilePartyAi>>();

    static readonly Action<MobilePartyAi, AiBehavior> DefaultBehavior = typeof(MobilePartyAi)
        .GetProperty(nameof(MobilePartyAi.DefaultBehavior)).GetSetMethod(true)
        .BuildDelegate<Action<MobilePartyAi, AiBehavior>>();

    static readonly Action<MobileParty, Settlement> TargetSettlement = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetSettlement)).GetSetMethod(true)
        .BuildDelegate<Action<MobileParty, Settlement>>();

    static readonly Action<MobileParty, MobileParty> TargetParty = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetParty)).GetSetMethod(true)
        .BuildDelegate<Action<MobileParty, MobileParty>>();

    static readonly Action<MobileParty, Vec2> TargetPosition = typeof(MobileParty)
        .GetProperty(nameof(MobileParty.TargetPosition)).GetSetMethod(true)
        .BuildDelegate < Action <MobileParty, Vec2>>();
}