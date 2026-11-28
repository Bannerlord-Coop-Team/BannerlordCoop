using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Hideouts.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Hideouts.Patches;

/// <summary>Sends the hideout mission's roguery xp to the server, which gives it to the raiding player heroes.</summary>
[HarmonyPatch(typeof(DefaultSkillLevelingManager))]
internal class HideoutRogueryXpPatches
{
    // Native gives it to Hero.MainHero on the battle host, and a client can't write skill xp.
    [HarmonyPatch(nameof(DefaultSkillLevelingManager.OnHideoutMissionEnd))]
    [HarmonyPrefix]
    private static bool OnHideoutMissionEndPrefix(bool isSucceeded)
        => PublishReward(isSucceeded ? HideoutRogueryXpReward.MissionSucceeded : HideoutRogueryXpReward.MissionFailed);

    [HarmonyPatch(nameof(DefaultSkillLevelingManager.OnHideoutClearedAsGhost))]
    [HarmonyPrefix]
    private static bool OnHideoutClearedAsGhostPrefix() => PublishReward(HideoutRogueryXpReward.ClearedAsGhost);

    private static bool PublishReward(HideoutRogueryXpReward reward)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        var mapEvent = MapEvent.PlayerMapEvent;
        if (mapEvent != null)
            MessageBroker.Instance.Publish(mapEvent, new HideoutRogueryXpEarned(mapEvent, reward));
        return false;
    }
}
