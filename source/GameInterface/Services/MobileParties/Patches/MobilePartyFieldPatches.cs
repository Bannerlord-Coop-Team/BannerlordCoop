using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Fields.Events;
using GameInterface.Services.PartyComponents;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
public class MobilePartyFieldPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyFieldPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(MobileParty)))
        {
            yield return method;
        }
        ;
        yield return AccessTools.Method(typeof(DefaultClanFinanceModel), nameof(DefaultClanFinanceModel.ApplyMoraleEffect));
        yield return AccessTools.Method(typeof(MobilePartyAi), nameof(MobilePartyAi.GetFleeBehavior));
    }
    
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AttachedToTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._attachedTo));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(AttachedToIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void AttachedToIntercept(MobileParty instance, MobileParty newAttachedTo)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._attachedTo = newAttachedTo;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._attachedTo = newAttachedTo;
            return;
        }

        MessageBroker.Instance.Publish(instance, new AttachedToChanged(newAttachedTo.StringId, instance.StringId));

        instance._attachedTo = newAttachedTo;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HasUnpaidWagesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty.HasUnpaidWages));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(HasUnpaidWagesIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void HasUnpaidWagesIntercept(MobileParty instance, float newHasUnpaidWages)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.HasUnpaidWages = newHasUnpaidWages;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance.HasUnpaidWages = newHasUnpaidWages;
            return;
        }

        MessageBroker.Instance.Publish(instance, new HasUnpaidWagesChanged(newHasUnpaidWages, instance.StringId));

        instance.HasUnpaidWages = newHasUnpaidWages;
    }
    
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LastCalculatedSpeedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._lastCalculatedSpeed));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(LastCalculatedSpeedIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void LastCalculatedSpeedIntercept(MobileParty instance, float newLastCalculatedSpeed)
    {
        var hasChanged = instance._lastCalculatedSpeed.CompareTo(newLastCalculatedSpeed) != 0;

        if (!hasChanged)
        {
            return;
        }

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._lastCalculatedSpeed = newLastCalculatedSpeed;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._lastCalculatedSpeed = newLastCalculatedSpeed;
            return;
        }

        MessageBroker.Instance.Publish(instance, new LastCalculatedSpeedChanged(newLastCalculatedSpeed, instance.StringId));

        instance._lastCalculatedSpeed = newLastCalculatedSpeed;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisorganizedUntilTimeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._disorganizedUntilTime));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(DisorganizedUntilTimeIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void DisorganizedUntilTimeIntercept(MobileParty instance, CampaignTime newDisorganizedUntilTime)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._disorganizedUntilTime = newDisorganizedUntilTime;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._disorganizedUntilTime = newDisorganizedUntilTime;
            return;
        }

        MessageBroker.Instance.Publish(instance, new DisorganizedUntilTimeChanged(newDisorganizedUntilTime.NumTicks, instance.StringId));

        instance._disorganizedUntilTime = newDisorganizedUntilTime;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartyPureSpeedLastCheckVersionTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyPureSpeedLastCheckVersion));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(PartyPureSpeedLastCheckVersionIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void PartyPureSpeedLastCheckVersionIntercept(MobileParty instance, int newPartyPureSpeedLastCheckVersion)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyPureSpeedLastCheckVersion = newPartyPureSpeedLastCheckVersion;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partyPureSpeedLastCheckVersion = newPartyPureSpeedLastCheckVersion;
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyPureSpeedLastCheckVersionChanged(newPartyPureSpeedLastCheckVersion, instance.StringId));

        instance._partyPureSpeedLastCheckVersion = newPartyPureSpeedLastCheckVersion;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartyLastCheckIsPrisonerTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyLastCheckIsPrisoner));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(PartyLastCheckIsPrisonerIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void PartyLastCheckIsPrisonerIntercept(MobileParty instance, bool newPartyLastCheckIsPrisoner)
    {
        var hasChanged = instance._partyLastCheckIsPrisoner != newPartyLastCheckIsPrisoner;

        if (!hasChanged)
        {
            return;
        }
        
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyLastCheckIsPrisoner = newPartyLastCheckIsPrisoner;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partyLastCheckIsPrisoner = newPartyLastCheckIsPrisoner;
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyLastCheckIsPrisonerChanged(newPartyLastCheckIsPrisoner, instance.StringId));

        instance._partyLastCheckIsPrisoner = newPartyLastCheckIsPrisoner;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LastCalculatedBaseSpeedExplainedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._lastCalculatedBaseSpeedExplained));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(LastCalculatedBaseSpeedExplainedIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void LastCalculatedBaseSpeedExplainedIntercept(MobileParty instance, ExplainedNumber newLastCalculatedBaseSpeedExplained)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._lastCalculatedBaseSpeedExplained = newLastCalculatedBaseSpeedExplained;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._lastCalculatedBaseSpeedExplained = newLastCalculatedBaseSpeedExplained;
            return;
        }

        MessageBroker.Instance.Publish(instance, new LastCalculatedBaseSpeedExplainedChanged(newLastCalculatedBaseSpeedExplained.BaseNumber, newLastCalculatedBaseSpeedExplained.IncludeDescriptions, newLastCalculatedBaseSpeedExplained._explainer?.BaseLine?.Name, instance.StringId));

        instance._lastCalculatedBaseSpeedExplained = newLastCalculatedBaseSpeedExplained;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartyLastCheckAtNightTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyLastCheckAtNight));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(PartyLastCheckAtNightIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void PartyLastCheckAtNightIntercept(MobileParty instance, bool newPartyLastCheckAtNight)
    {
        var hasChanged = instance._partyLastCheckAtNight != newPartyLastCheckAtNight;

        if (!hasChanged)
        {
            return;
        }
        
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyLastCheckAtNight = newPartyLastCheckAtNight;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partyLastCheckAtNight = newPartyLastCheckAtNight;
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyLastCheckAtNightChanged(newPartyLastCheckAtNight, instance.StringId));

        instance._partyLastCheckAtNight = newPartyLastCheckAtNight;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ItemRosterVersionNoTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._itemRosterVersionNo));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(ItemRosterVersionNoIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void ItemRosterVersionNoIntercept(MobileParty instance, int newItemRosterVersionNo)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._itemRosterVersionNo = newItemRosterVersionNo;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._itemRosterVersionNo = newItemRosterVersionNo;
            return;
        }

        MessageBroker.Instance.Publish(instance, new ItemRosterVersionNoChanged(newItemRosterVersionNo, instance.StringId));

        instance._itemRosterVersionNo = newItemRosterVersionNo;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartySizeRatioLastCheckVersionTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partySizeRatioLastCheckVersion));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(PartySizeRatioLastCheckVersionIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void PartySizeRatioLastCheckVersionIntercept(MobileParty instance, int newPartySizeRatioLastCheckVersion)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partySizeRatioLastCheckVersion = newPartySizeRatioLastCheckVersion;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partySizeRatioLastCheckVersion = newPartySizeRatioLastCheckVersion;
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartySizeRatioLastCheckVersionChanged(newPartySizeRatioLastCheckVersion, instance.StringId));

        instance._partySizeRatioLastCheckVersion = newPartySizeRatioLastCheckVersion;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LatestUsedPaymentRatioTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._latestUsedPaymentRatio));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(LatestUsedPaymentRatioIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void LatestUsedPaymentRatioIntercept(MobileParty instance, int newLatestUsedPaymentRatio)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._latestUsedPaymentRatio = newLatestUsedPaymentRatio;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._latestUsedPaymentRatio = newLatestUsedPaymentRatio;
            return;
        }

        MessageBroker.Instance.Publish(instance, new LatestUsedPaymentRatioChanged(newLatestUsedPaymentRatio, instance.StringId));

        instance._latestUsedPaymentRatio = newLatestUsedPaymentRatio;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CachedPartySizeRatioTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._cachedPartySizeRatio));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(CachedPartySizeRatioIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void CachedPartySizeRatioIntercept(MobileParty instance, float newCachedPartySizeRatio)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._cachedPartySizeRatio = newCachedPartySizeRatio;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._cachedPartySizeRatio = newCachedPartySizeRatio;
            return;
        }

        MessageBroker.Instance.Publish(instance, new CachedPartySizeRatioChanged(newCachedPartySizeRatio, instance.StringId));

        instance._cachedPartySizeRatio = newCachedPartySizeRatio;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CachedPartySizeLimitTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._cachedPartySizeLimit));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(CachedPartySizeLimitIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void CachedPartySizeLimitIntercept(MobileParty instance, int newCachedPartySizeLimit)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._cachedPartySizeLimit = newCachedPartySizeLimit;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._cachedPartySizeLimit = newCachedPartySizeLimit;
            return;
        }

        MessageBroker.Instance.Publish(instance, new CachedPartySizeLimitChanged(newCachedPartySizeLimit, instance.StringId));

        instance._cachedPartySizeLimit = newCachedPartySizeLimit;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DoNotAttackMainPartyTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._doNotAttackMainParty));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(DoNotAttackMainPartyIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void DoNotAttackMainPartyIntercept(MobileParty instance, int newDoNotAttackMainParty)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._doNotAttackMainParty = newDoNotAttackMainParty;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._doNotAttackMainParty = newDoNotAttackMainParty;
            return;
        }

        MessageBroker.Instance.Publish(instance, new DoNotAttackMainPartyChanged(newDoNotAttackMainParty, instance.StringId));

        instance._doNotAttackMainParty = newDoNotAttackMainParty;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CustomHomeSettlementTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._customHomeSettlement));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(CustomHomeSettlementIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void CustomHomeSettlementIntercept(MobileParty instance, Settlement newCustomHomeSettlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._customHomeSettlement = newCustomHomeSettlement;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._customHomeSettlement = newCustomHomeSettlement;
            return;
        }

        MessageBroker.Instance.Publish(instance, new CustomHomeSettlementChanged(newCustomHomeSettlement?.StringId, instance.StringId));

        instance._customHomeSettlement = newCustomHomeSettlement;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IsDisorganizedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isDisorganized));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(IsDisorganizedIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void IsDisorganizedIntercept(MobileParty instance, bool newIsDisorganized)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._isDisorganized = newIsDisorganized;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._isDisorganized = newIsDisorganized;
            return;
        }

        MessageBroker.Instance.Publish(instance, new IsDisorganizedChanged(newIsDisorganized, instance.StringId));

        instance._isDisorganized = newIsDisorganized;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IsCurrentlyUsedByAQuestTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isCurrentlyUsedByAQuest));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(IsCurrentlyUsedByAQuestIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void IsCurrentlyUsedByAQuestIntercept(MobileParty instance, bool newIsCurrentlyUsedByAQuest)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._isCurrentlyUsedByAQuest = newIsCurrentlyUsedByAQuest;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._isCurrentlyUsedByAQuest = newIsCurrentlyUsedByAQuest;
            return;
        }

        MessageBroker.Instance.Publish(instance, new IsCurrentlyUsedByAQuestChanged(newIsCurrentlyUsedByAQuest, instance.StringId));

        instance._isCurrentlyUsedByAQuest = newIsCurrentlyUsedByAQuest;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartyTradeGoldTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyTradeGold));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(PartyTradeGoldIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void PartyTradeGoldIntercept(MobileParty instance, int newPartyTradeGold)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyTradeGold = newPartyTradeGold;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partyTradeGold = newPartyTradeGold;
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyTradeGoldChanged(newPartyTradeGold, instance.StringId));

        instance._partyTradeGold = newPartyTradeGold;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IgnoredUntilTimeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._ignoredUntilTime));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(IgnoredUntilTimeIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void IgnoredUntilTimeIntercept(MobileParty instance, CampaignTime newIgnoredUntilTime)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._ignoredUntilTime = newIgnoredUntilTime;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._ignoredUntilTime = newIgnoredUntilTime;
            return;
        }

        MessageBroker.Instance.Publish(instance, new IgnoredUntilTimeChanged(newIgnoredUntilTime.NumTicks, instance.StringId));

        instance._ignoredUntilTime = newIgnoredUntilTime;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AverageFleeTargetDirectionTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty.AverageFleeTargetDirection));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(AverageFleeTargetDirectionIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void AverageFleeTargetDirectionIntercept(MobileParty instance, Vec2 newAverageFleeTargetDirection)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.AverageFleeTargetDirection = newAverageFleeTargetDirection;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance.AverageFleeTargetDirection = newAverageFleeTargetDirection;
            return;
        }

        MessageBroker.Instance.Publish(instance, new AverageFleeTargetDirectionChanged(newAverageFleeTargetDirection.X, newAverageFleeTargetDirection.Y, instance.StringId));

        instance.AverageFleeTargetDirection = newAverageFleeTargetDirection;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> BesiegerCampResetStartedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._besiegerCampResetStarted));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(BesiegerCampResetStartedIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void BesiegerCampResetStartedIntercept(MobileParty instance, bool newBesiegerCampResetStarted)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._besiegerCampResetStarted = newBesiegerCampResetStarted;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._besiegerCampResetStarted = newBesiegerCampResetStarted;
            return;
        }

        MessageBroker.Instance.Publish(instance, new BesiegerCampResetStartedChanged(newBesiegerCampResetStarted, instance.StringId));

        instance._besiegerCampResetStarted = newBesiegerCampResetStarted;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LastWeatherTerrainEffectTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._lastWeatherTerrainEffect));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyFieldPatches), nameof(LastWeatherTerrainEffectIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void LastWeatherTerrainEffectIntercept(MobileParty instance, MapWeatherModel.WeatherEventEffectOnTerrain newLastWeatherTerrainEffect)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._lastWeatherTerrainEffect = newLastWeatherTerrainEffect;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._lastWeatherTerrainEffect = newLastWeatherTerrainEffect;
            return;
        }

        MessageBroker.Instance.Publish(instance, new LastWeatherTerrainEffectChanged((int) newLastWeatherTerrainEffect, instance.StringId));

        instance._lastWeatherTerrainEffect = newLastWeatherTerrainEffect;
    }
}
