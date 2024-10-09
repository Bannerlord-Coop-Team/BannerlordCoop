using GameInterface.Policies;
using GameInterface.Services.Towns.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

/// <summary>
/// NOT Disabled since this spawns parties and heroes at the beginning of the game
/// </summary>
[HarmonyPatch]
internal class DisableHeroSpawnCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(HeroSpawnCampaignBehavior));

    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
}
