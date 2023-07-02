using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Encounters.Patches;

[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class DisableEncounterGameMenuBehavior
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
