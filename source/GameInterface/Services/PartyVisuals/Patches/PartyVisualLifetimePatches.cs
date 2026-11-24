using Common;
using Common.Logging;
using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Interfaces;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>Replicates visual lifetime changes made by the graphical server's map.</summary>
[HarmonyPatch]
public class PartyVisualLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<PartyVisualLifetimePatches>();

    [HarmonyPatch(typeof(MobilePartyVisual), MethodType.Constructor, typeof(PartyBase))]
    [HarmonyPrefix]
    private static void CreatePartyVisualPrefix(PartyBase partyBase)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsClient) return;
        if (partyBase.MobileParty is { } party &&
            ContainerProvider.TryResolve<IPartyVisualInterface>(out var partyVisuals))
        {
            try
            {
                partyVisuals.OnNativeVisualCreated(party);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to replicate visual creation for {Party}", party.StringId);
            }
        }
    }

    [HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.OnPartyRemoved))]
    [HarmonyPostfix]
    private static void OnMobilePartyDestroyedPostfix(MobilePartyVisual __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(MobilePartyVisual));
            return;
        }

        if (__instance.MapEntity.MobileParty is { } party &&
            ContainerProvider.TryResolve<IPartyVisualInterface>(out var partyVisuals))
        {
            try
            {
                partyVisuals.OnNativeVisualRemoved(party);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to replicate visual removal for {Party}", party.StringId);
            }
        }
    }
}
