using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Rafts.Interfaces;
using GameInterface.Services.Rafts.Messages;

namespace GameInterface.Services.Rafts.Handlers;

internal class PlayerRaftHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerRaftFleetInterface raftFleetInterface;

    public PlayerRaftHandler(IMessageBroker messageBroker, IPlayerRaftFleetInterface raftFleetInterface)
    {
        this.messageBroker = messageBroker;
        this.raftFleetInterface = raftFleetInterface;

        messageBroker.Subscribe<PlayerPartyReleasedFromCaptivity>(Handle);
        messageBroker.Subscribe<PlayerCharacterChangedOnServer>(Handle_PlayerCharacterChanged);
        messageBroker.Subscribe<PlayerRaftLandingAttempted>(Handle_RaftLanding);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerPartyReleasedFromCaptivity>(Handle);
        messageBroker.Unsubscribe<PlayerCharacterChangedOnServer>(Handle_PlayerCharacterChanged);
        messageBroker.Unsubscribe<PlayerRaftLandingAttempted>(Handle_RaftLanding);
    }

    private void Handle_RaftLanding(MessagePayload<PlayerRaftLandingAttempted> obj)
    {
        var party = obj.What.PlayerParty;

        raftFleetInterface.PutAshore(party);

        // Clears the client's raft move target too, which would otherwise keep heading for the old shore
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: false, resetMovementToHold: true));
    }

    private void Handle_PlayerCharacterChanged(MessagePayload<PlayerCharacterChangedOnServer> obj)
    {
        if (!ModInformation.IsNavalEnabled) return;

        // Vanilla RaftStateCampaignBehavior.OnPlayerCharacterChanged only looks at MobileParty.MainParty's cache, which the server doesn't have
        raftFleetInterface.OnPlayerCharacterChanged(obj.What.NewPlayerHero, obj.What.NewMainParty);
    }

    private void Handle(MessagePayload<PlayerPartyReleasedFromCaptivity> obj)
    {
        if (!ModInformation.IsNavalEnabled) return;

        var party = obj.What.PlayerParty;

        raftFleetInterface.StrandAfterRelease(party);

        // Published after the release has placed and stopped the party, so the raft's move to land isn't reset
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));
    }
}
