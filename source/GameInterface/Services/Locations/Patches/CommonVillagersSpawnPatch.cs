using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Re-enables only the ambient villager crowd on clients. The behavior's <c>RegisterEvents</c> also
/// wires session-launched and settlement-owner-change handlers that are not co-op safe, so this
/// subscribes just the scene-spawn listener and skips the original. The crowd is kept identical on
/// every client by <see cref="AmbientSpawnSeedPatch"/>. The dedicated host never runs a mission scene,
/// so it stays fully disabled (as before).
/// </summary>
[HarmonyPatch(typeof(CommonVillagersCampaignBehavior), nameof(CommonVillagersCampaignBehavior.RegisterEvents))]
internal class CommonVillagersSpawnPatch
{
    static bool Prefix(CommonVillagersCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            AmbientSpawnReenable.SubscribeSpawnListenerOnly(__instance);
        }

        return false;
    }
}
