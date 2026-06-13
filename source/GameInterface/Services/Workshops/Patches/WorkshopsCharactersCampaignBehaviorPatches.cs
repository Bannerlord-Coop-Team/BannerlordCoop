using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(WorkshopsCharactersCampaignBehavior))]
internal class DisableWorkshopsCharactersCampaignBehavior
{
    // Needs to run on the client
    // Includes logic for dialogue and other interactions with workshops
    [HarmonyPatch(nameof(WorkshopsCharactersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => true;
}
