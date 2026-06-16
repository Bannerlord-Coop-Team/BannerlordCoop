using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Re-enables only the ambient workshop-worker crowd on clients. The behavior's <c>RegisterEvents</c>
/// also wires session-launched and conversation-ended handlers that are not co-op safe, so this
/// subscribes just the scene-spawn listener and skips the original. The crowd is kept identical on
/// every client by <see cref="AmbientSpawnSeedPatch"/>. The dedicated host never runs a mission scene,
/// so it stays fully disabled (as before).
/// </summary>
[HarmonyPatch(typeof(WorkshopsCharactersCampaignBehavior), nameof(WorkshopsCharactersCampaignBehavior.RegisterEvents))]
internal class WorkshopsCharactersSpawnPatch
{
    static bool Prefix(WorkshopsCharactersCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            AmbientSpawnReenable.SubscribeSpawnListenerOnly(__instance);
        }

        return false;
    }
}
