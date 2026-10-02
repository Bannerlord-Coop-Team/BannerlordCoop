#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Commands;

[ProtoContract(SkipConstructor = true)]
public record NetworkIssue1837Continuation : ICommand
{
    [ProtoMember(1)] public string ControllerId { get; }
    [ProtoMember(2)] public string SettlementId { get; }
    [ProtoMember(3)] public string Action { get; }

    public NetworkIssue1837Continuation(string controllerId, string settlementId, string action)
    {
        ControllerId = controllerId;
        SettlementId = settlementId;
        Action = action;
    }
}

public sealed class Issue1837ContinuationCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;

    public Issue1837ContinuationCoopCommand(IObjectManager objectManager, IPlayerManager playerManager,
        INetwork network, IMessageBroker messageBroker, IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
    }

    public string Prefix => "coop.debug.siege";
    public string Name => "issue1837_continue";
    public string Description => "Server-authorized Danustica witness setup or normal client request.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controllerId", "The connected player controller id."),
        new ExpectedArgs("action", "gate, enter, or assault."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || !playerManager.TryGetPlayer(args[0], out var player) ||
            !playerManager.IsConnected(player) || !playerManager.TryGetPeer(args[0], out var peer) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            !objectManager.TryGetObject<Settlement>("town_ES1", out var settlement) ||
            party.Army != null || party.MapEvent != null || party.CurrentSettlement != null)
            return new CoopCommandResult(false, "Server, connected free player, and Danustica are required.", "command_failed");

        if (args[1] == "gate")
        {
            if (party.SiegeEvent != null || !behaviorSnapshot.TryCreate(party, out var data))
                return new CoopCommandResult(false, "Cannot stage a busy player.", "command_failed");
            data.PartyPosition = settlement.GatePosition;
            data.ForcePosition = true;
            data.IsCurrentlyAtSea = false;
            data.ResetMovementToHold = true;
            messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
        }
        else if (args[1] == "enter" && party.BesiegerCamp == null && settlement.SiegeEvent == null &&
            party.Position.ToVec2().DistanceSquared(settlement.GatePosition.ToVec2()) <= 1f)
        {
            network.Send(peer, new NetworkIssue1837Continuation(args[0], settlement.StringId, "enter"));
        }
        else if (args[1] == "assault" && party.BesiegerCamp?.LeaderParty == party &&
            party.BesiegedSettlement == settlement && party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true)
        {
            network.Send(peer, new NetworkIssue1837Continuation(args[0], settlement.StringId, "assault"));
        }
        else return new CoopCommandResult(false, "Action or production preconditions do not match.", "command_failed");
        return new CoopCommandResult(true, "Requested continuation; observe the production result separately.");
    }
}
#endif
