using System;
using System.Collections.Generic;
using System.Reflection;
using Common.Logging;
using Common.Messaging;
using Common.Messaging.MBTypes;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages.Properties;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroPropertiesPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroPropertiesPatches>();
    
    [HarmonyPatch(nameof(Hero.StaticBodyProperties), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetStaticBodyPropertiesPrefix(Hero __instance, StaticBodyProperties value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.StaticBodyProperties), Environment.StackTrace);
            return false;
        }

        var message = new StaticBodyPropertiesChanged(__instance.StringId, nameof(Hero.StaticBodyProperties), value.KeyPart1, value.KeyPart2, value.KeyPart3, value.KeyPart4, value.KeyPart5, value.KeyPart6, value.KeyPart7, value.KeyPart8);
        MessageBroker.Instance.Publish(__instance, message);


        return ModInformation.IsServer;
    }
    
    
    [HarmonyPatch(nameof(Hero.Weight), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetWeightPrefix(Hero __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.Weight), Environment.StackTrace);
            return false;
        }

        var message = new GenericChangedEvent<HeroPropertiesHandler>(__instance.StringId, value, nameof(Hero.Weight));
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Hero.Build), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetBuildPrefix(Hero __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.Build), Environment.StackTrace);
            return false;
        }

        var message = new GenericChangedEvent<HeroPropertiesHandler>(__instance.StringId, value, nameof(Hero.Build));
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Hero.PassedTimeAtHomeSettlement), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetPassedTimeAtHomeSettlementPrefix(Hero __instance, float value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.PassedTimeAtHomeSettlement), Environment.StackTrace);
            return false;
        }

        var message = new GenericChangedEvent<HeroPropertiesHandler>(__instance.StringId, value, nameof(Hero.PassedTimeAtHomeSettlement));
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Hero.EncyclopediaText), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetEncyclopediaTextPrefix(Hero __instance, TextObject value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.EncyclopediaText), Environment.StackTrace);
            return false;
        }

        if (value != null)
        {
            var message = new TextObjectChangedEvent<HeroPropertiesHandler>(__instance.StringId, value.Value, nameof(Hero.EncyclopediaText));
            MessageBroker.Instance.Publish(__instance, message);
        }

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Hero.IsFemale), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetIsFemalePrefix(Hero __instance, bool value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero.IsFemale), Environment.StackTrace);
            return false;
        }

        var message = new GenericChangedEvent<HeroPropertiesHandler>(__instance.StringId, value, nameof(Hero.IsFemale));
        MessageBroker.Instance.Publish(__instance, message);

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Hero._battleEquipment), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool SetBattleEquipmentPrefix(Hero __instance, Equipment value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to set {name}\n"
                         + "Callstack: {callstack}", nameof(Hero._battleEquipment), Environment.StackTrace);
            return false;
        }

        if (__instance.StringId != null && value != null && value._itemSlots != null)
        {
            var message = new GenericChangedEvent<HeroPropertiesHandler, string>(__instance.StringId, value.CalculateEquipmentCode(), nameof(Hero._battleEquipment));
            MessageBroker.Instance.Publish(__instance, message, nameof(Equipment));
        }

        return ModInformation.IsServer;
    }
}