using Common;
using System;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public interface IDisconnectedOwnerEvaluationSupport
{
    bool TryEvaluateOnBehalfOfDisconnectedOwner(Hero questGiver, Action<string> evaluate);
}

public sealed class DisconnectedOwnerEvaluationSupport : IDisconnectedOwnerEvaluationSupport
{
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public DisconnectedOwnerEvaluationSupport(IIssueOwnershipRegistry ownershipRegistry, IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.ownershipRegistry = ownershipRegistry;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    public bool TryEvaluateOnBehalfOfDisconnectedOwner(Hero questGiver, Action<string> evaluate)
    {
        if (ModInformation.IsClient) return false;
        if (!ownershipRegistry.TryGetOwnerControllerId(questGiver, out var ownerControllerId)) return false;
        if (!playerManager.TryGetPlayer(ownerControllerId, out var player) ||
            playerManager.IsConnected(player)) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var ownerHero)) return false;

        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var ownerParty);

        using (new MainHeroSubstitutionScope(ownerHero, ownerParty))
        {
            evaluate(ownerControllerId);
        }

        return true;
    }
}
