using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Need to run these methods within AllowedThreads for local modifications to Hero.MainHero.HeroDeveloper
/// </summary>
[HarmonyPatch(typeof(CharacterCreationContent))]
internal class AllowCharacterCreationContentPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(CharacterCreationContent), nameof(CharacterCreationContent.ApplySkillAndAttributeEffects)),
        AccessTools.Method(typeof(CharacterCreationContent), nameof(CharacterCreationContent.SetMainHeroInitialStats))
    };

    [HarmonyPrefix]
    private static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyFinalizer]
    private static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}
