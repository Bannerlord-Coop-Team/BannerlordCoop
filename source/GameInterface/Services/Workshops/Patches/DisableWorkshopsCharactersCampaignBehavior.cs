using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(WorkshopsCharactersCampaignBehavior))]
internal class DisableWorkshopsCharactersCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCharactersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
