using Common;
using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public static class DisconnectedOwnerEvaluationSupport
{
    public static bool TryEvaluateOnBehalfOfDisconnectedOwner(Hero questGiver, Action<string> evaluate)
    {
        if (!ModInformation.IsServer) return false;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) ||
            !ownershipRegistry.TryGetOwnerControllerId(questGiver, out var ownerControllerId)) return false;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(ownerControllerId, out var player) ||
            playerManager.IsConnected(player)) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var ownerHero)) return false;

        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var ownerParty);

        using (new MainHeroSubstitutionScope(ownerHero, ownerParty))
        {
            evaluate(ownerControllerId);
        }

        return true;
    }
}
