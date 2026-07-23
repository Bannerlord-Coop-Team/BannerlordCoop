using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch]
internal class GamePatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        return new MethodBase[]
        {
            AccessTools.Method(typeof(Game), "InitializeParameters"),
            AccessTools.Method(typeof(NativeConfig), "OnConfigChanged"),
            AccessTools.Method(typeof(GameTextManager), "LoadGameTexts"),
            AccessTools.Method(typeof(ConversationAnimationManager), "LoadConversationAnimData"),
            AccessTools.Method(typeof(ModuleHelper), "GetModuleFullPath"),
            //AccessTools.Method(typeof(MBGameManager), "InitializeGameStarter"),
            AccessTools.Method(typeof(MBGameManager), "OnGameStart"),
            AccessTools.Method(typeof(Game), "SetBasicModels"),
            //AccessTools.Method(typeof(MBGameManager), "BeginGameStart"),
            AccessTools.Method(typeof(MBObjectManager), "LoadXML"),
            AccessTools.Method(typeof(Campaign), "InitializeDefaultEquipments"),
        };
    }

    static bool Prefix() => false;
}
