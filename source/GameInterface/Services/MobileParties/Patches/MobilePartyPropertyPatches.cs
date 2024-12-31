using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Patches;

public enum PropertyType
{
    Army,
    CustomName,
    LastVisitedSettlement,
    Aggressiveness,
    Objective,
    //Ai,
    IsActive,
    ShortTermBehaviour,
    IsPartyTradeActive,
    PartyTradeGold,
    PartyTradeTaxGold,
    StationaryStartTime,
    VersionNo,
    ShouldJoinPlayerBattles,
    IsDisbanding,
    CurrentSettlement,
    AttachedTo,
    BesiegerCamp,
    Scout,
    Engineer,
    Quartermaster,
    Surgeon,
    ActualClan,
    RecentEventsMorale,
    EventPositionAdder,
    PartyComponent,
    IsMilita,
    IsLordParty,
    IsVillager,
    IsCaravan,
    IsGarrison,
    IsCustomParty,
    IsBandit,
}


[HarmonyPatch(typeof(MobileParty))]
public class MobilePartyPropertyPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyPropertyPatches>();

    [HarmonyPatch(nameof(MobileParty.Army), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetArmyPrefix(MobileParty __instance, Army value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Army), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Army, __instance.StringId, value?.GetStringId());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.CustomName), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetCustomNamePrefix(MobileParty __instance, TextObject value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.CustomName), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.CustomName, __instance.StringId, value?.Value);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.LastVisitedSettlement), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetLastVisitedSettlementPrefix(MobileParty __instance, Settlement value)
    {
        if (value == __instance.LastVisitedSettlement) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.LastVisitedSettlement), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.LastVisitedSettlement, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Aggressiveness), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetAggressivenessPrefix(MobileParty __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Aggressiveness), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Aggressiveness, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Objective), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetObjectivePrefix(MobileParty __instance, int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Objective), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Objective, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    //[HarmonyPatch(nameof(MobileParty.Ai), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetAiPrefix(MobileParty __instance, MobilePartyAi value)
    //{
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client tried to set {name}\n"
    //            + "Callstack: {callstack}", nameof(MobileParty.Ai), Environment.StackTrace);
    //        return false;
    //    }

    //    var message = new MobilePartyPropertyChanged(PropertyType.Ai, __instance.StringId, value._mobileParty?.StringId);
    //    MessageBroker.Instance.Publish(__instance, message);

    //    return ModInformation.IsServer;
    //}

    [HarmonyPatch(nameof(MobileParty.IsActive), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsActivePrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsActive), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsActive, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsPartyTradeActive), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsPartyTradeActivePrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsPartyTradeActive), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsPartyTradeActive, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.PartyTradeGold), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetPartyTradeGoldPrefix(MobileParty __instance, int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.PartyTradeGold), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.PartyTradeGold, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.PartyTradeTaxGold), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetPartyTradeTaxGoldPrefix(MobileParty __instance, int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.PartyTradeTaxGold), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.PartyTradeTaxGold, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    //StationaryStartTime causes a lot of lag, called on tick

    //[HarmonyPatch(nameof(MobileParty.StationaryStartTime), MethodType.Setter)]
    //[HarmonyPrefix]
    //private static bool SetStationaryStartTimePrefix(MobileParty __instance, CampaignTime value)
    //{
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client tried to set {name}\n"
    //            + "Callstack: {callstack}", nameof(MobileParty.StationaryStartTime), Environment.StackTrace);
    //        return true;
    //    }

    //    var message = new MobilePartyPropertyChanged(PropertyType.StationaryStartTime, __instance.StringId, value.NumTicks.ToString());
    //    MessageBroker.Instance.Publish(__instance, message);

    //    return ModInformation.IsServer;
    //}

    [HarmonyPatch(nameof(MobileParty.VersionNo), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetVersionNoPrefix(MobileParty __instance, int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.VersionNo), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.VersionNo, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.ShouldJoinPlayerBattles), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetShouldJoinPlayerBattlesPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.ShouldJoinPlayerBattles), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.ShouldJoinPlayerBattles, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsDisbanding), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsDisbandingPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsDisbanding), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsDisbanding, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.CurrentSettlement), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetCurrentSettlementPrefix(MobileParty __instance, Settlement value)
    {
        if (value == __instance._currentSettlement) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.CurrentSettlement), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.CurrentSettlement, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.AttachedTo), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetAttachedToPrefix(MobileParty __instance, MobileParty value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.AttachedTo), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.AttachedTo, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.BesiegerCamp), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetBesiegerCampPrefix(MobileParty __instance, BesiegerCamp value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.BesiegerCamp), Environment.StackTrace);
            return false;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (objectManager.TryGetId(value, out var besiegerCampId) == false) return true;

        var message = new MobilePartyPropertyChanged(PropertyType.BesiegerCamp, __instance.StringId, besiegerCampId);
        MessageBroker.Instance.Publish(__instance, message); 

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Scout), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetScoutPrefix(MobileParty __instance, Hero value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Scout), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Scout, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Engineer), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetEngineerPrefix(MobileParty __instance, Hero value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Engineer), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Engineer, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Quartermaster), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetQuartermasterPrefix(MobileParty __instance, Hero value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Quartermaster), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Quartermaster, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.Surgeon), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetSurgeonPrefix(MobileParty __instance, Hero value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.Surgeon), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.Surgeon, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.ActualClan), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetActualClanPrefix(MobileParty __instance, Clan value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.ActualClan), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.ActualClan, __instance.StringId, value?.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.RecentEventsMorale), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetRecentEventsMoralePrefix(MobileParty __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.RecentEventsMorale), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.RecentEventsMorale, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.EventPositionAdder), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetEventPositionAdderPrefix(MobileParty __instance, Vec2 value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.EventPositionAdder), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.EventPositionAdder, __instance.StringId, value.X.ToString(), value.Y.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.PartyComponent), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetPartyComponentPrefix(MobileParty __instance, PartyComponent value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.PartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.PartyComponent, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsMilitia), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsMilitaPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsMilitia), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsMilita, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsLordParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsLordPartyPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsLordParty), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsLordParty, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsVillager), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsVillagerPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsVillager), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsVillager, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsCaravan), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsCaravanPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsCaravan), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsCaravan, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsGarrison), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsGarrisonPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsGarrison), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsGarrison, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsCustomParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsCustomPartyPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsCustomParty), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsCustomParty, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MobileParty.IsBandit), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsBanditPrefix(MobileParty __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                + "Callstack: {callstack}", nameof(MobileParty.IsBandit), Environment.StackTrace);
            return false;
        }

        var message = new MobilePartyPropertyChanged(PropertyType.IsBandit, __instance.StringId, value.ToString());
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }
}