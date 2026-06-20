using HarmonyLib;
using StoryMode.GameComponents.CampaignBehaviors;

namespace GameInterface.Services.StoryMode.Patches.Disable;

// Story quests register NPC conversation dialogue that soft-locks in coop.
// Disabling RegisterEvents on the storyline + phase behaviors stops the quests
// (and their dialogue) from spawning. Re-enable when quests are supported.

[HarmonyPatch(typeof(MainStorylineCampaignBehavior), nameof(MainStorylineCampaignBehavior.RegisterEvents))]
internal class DisableMainStorylineCampaignBehavior
{
    static bool Prefix() => false;
}

[HarmonyPatch(typeof(TutorialPhaseCampaignBehavior), nameof(TutorialPhaseCampaignBehavior.RegisterEvents))]
internal class DisableTutorialPhaseCampaignBehavior
{
    static bool Prefix() => false;
}

[HarmonyPatch(typeof(FirstPhaseCampaignBehavior), nameof(FirstPhaseCampaignBehavior.RegisterEvents))]
internal class DisableFirstPhaseCampaignBehavior
{
    static bool Prefix() => false;
}

[HarmonyPatch(typeof(SecondPhaseCampaignBehavior), nameof(SecondPhaseCampaignBehavior.RegisterEvents))]
internal class DisableSecondPhaseCampaignBehavior
{
    static bool Prefix() => false;
}

[HarmonyPatch(typeof(ThirdPhaseCampaignBehavior), nameof(ThirdPhaseCampaignBehavior.RegisterEvents))]
internal class DisableThirdPhaseCampaignBehavior
{
    static bool Prefix() => false;
}
