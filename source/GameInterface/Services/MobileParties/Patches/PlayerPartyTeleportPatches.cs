using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Announces client cheat teleports through the authoritative party movement channel.
/// </summary>
[HarmonyPatch(typeof(MapScreen), nameof(MapScreen.HandleLeftMouseButtonClick))]
internal static class PlayerPartyTeleportPatches
{
    private static void Prefix(out CampaignVec2 __state)
    {
        __state = MobileParty.MainParty.Position;
    }

    private static void Postfix(MapScreen __instance, MapEntityVisual visualOfSelectedEntity, CampaignVec2 __state)
    {
        var mainParty = MobileParty.MainParty;
        if (ModInformation.IsClient &&
            Game.Current.CheatMode &&
            __instance.Input.IsControlDown() &&
            visualOfSelectedEntity == null &&
            mainParty?.Ai != null &&
            mainParty.Position != __state)
        {
            MessageBroker.Instance.Publish(
                mainParty,
                new PartyBehaviorChangeAttempted(
                    mainParty,
                    forcePosition: true,
                    isCurrentlyAtSea: mainParty.IsCurrentlyAtSea,
                    resetMovementToHold: true));
        }
    }
}
