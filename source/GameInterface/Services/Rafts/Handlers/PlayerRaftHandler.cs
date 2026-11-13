using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players.Messages;
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
        messageBroker.Subscribe<PlayerRaftResumeAttempted>(Handle_RaftResume);
        messageBroker.Subscribe<PartyOccupancyChanged>(Handle_OccupancyChanged);
        messageBroker.Subscribe<PlayerPartyRestoredOnReconnect>(Handle_PartyRestoredOnReconnect);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerPartyReleasedFromCaptivity>(Handle);
        messageBroker.Unsubscribe<PlayerCharacterChangedOnServer>(Handle_PlayerCharacterChanged);
        messageBroker.Unsubscribe<PlayerRaftResumeAttempted>(Handle_RaftResume);
        messageBroker.Unsubscribe<PartyOccupancyChanged>(Handle_OccupancyChanged);
        messageBroker.Unsubscribe<PlayerPartyRestoredOnReconnect>(Handle_PartyRestoredOnReconnect);
    }

    private void Handle_RaftResume(MessagePayload<PlayerRaftResumeAttempted> obj)
    {
        var party = obj.What.PlayerParty;

        if (!raftFleetInterface.ResumeRaft(party)) return;
        
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));
    }

    // The move to shore a raft got while its party was parked offline doesn't survive, so start it again once the party is back
    private void Handle_PartyRestoredOnReconnect(MessagePayload<PlayerPartyRestoredOnReconnect> obj)
    {
        var party = obj.What.PlayerParty;

        if (!raftFleetInterface.ResumeRaft(party)) return;

        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));
    }

    private void Handle_OccupancyChanged(MessagePayload<PartyOccupancyChanged> obj)
    {
        var party = obj.What.MobileParty;

        // A raft's AI is off, so leaving a battle would hold it at sea for good.
        GameThread.RunSafe(() =>
        {
            if (!party.IsInRaftState || !party.IsCurrentlyAtSea || party.MapEvent != null || party.CurrentSettlement != null) return;

            if (!raftFleetInterface.ResumeRaft(party)) return;

            messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));
        });
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
