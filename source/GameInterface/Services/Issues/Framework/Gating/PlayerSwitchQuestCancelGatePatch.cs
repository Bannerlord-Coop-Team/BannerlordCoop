using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Gating;

// The join-time hero switch runs while originals are allowed, and vanilla would cancel every ongoing quest in the save, including other players'
[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class PlayerSwitchQuestCancelGatePatch
{
    // Set around ChangePlayerCharacterAction.Apply in HeroInterface.SwitchToPlayer; game-thread only
    internal static bool SuppressForPlayerSwitch;

    [HarmonyPrefix]
    internal static bool Prefix()
    {
        return !SuppressForPlayerSwitch;
    }
}
