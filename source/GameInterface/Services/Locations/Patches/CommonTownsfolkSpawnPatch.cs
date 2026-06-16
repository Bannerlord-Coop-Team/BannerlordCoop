using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Re-enables the ambient townsfolk crowd on clients. This behavior's <c>RegisterEvents</c> wires only the
/// scene-spawn listener, but it is re-subscribed through <see cref="AmbientSpawnReenable"/> (like the other
/// ambient behaviors) so the crowd is scoped as ambient - made static and non-interactable - and kept identical
/// across clients by <see cref="AmbientSpawnSeedPatch"/>. The dedicated host never runs a mission scene, so it
/// stays disabled.
/// </summary>
[HarmonyPatch(typeof(CommonTownsfolkCampaignBehavior), nameof(CommonTownsfolkCampaignBehavior.RegisterEvents))]
internal class CommonTownsfolkSpawnPatch
{
    static bool Prefix(CommonTownsfolkCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            AmbientSpawnReenable.SubscribeSpawnListenerOnly(__instance);
        }

        return false;
    }
}
