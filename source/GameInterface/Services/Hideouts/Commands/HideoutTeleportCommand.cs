using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Utils.Commands;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Hideouts.Commands;

public interface IHideoutTeleportCommand : ICoopCommand
{
}

public sealed class HideoutTeleportCommand : IHideoutTeleportCommand
{
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly ICommandObjectResolver resolver;

    public HideoutTeleportCommand(IMessageBroker messageBroker, IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ICommandObjectResolver resolver)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (behaviorSnapshot == null) throw new ArgumentNullException(nameof(behaviorSnapshot));
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.resolver = resolver;
    }

    public string Prefix => "coop.debug.hideout";
    public string Name => "teleport_party";
    public string Description => "Reveals the nearest populated hideout off cooldown and teleports the party with the given id to its entrance.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("partyId", "Party name or id (ids shown by coop.debug.players.list); quote names with spaces."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient)
            return new CoopCommandResult(false, "Run this command on the server.", "server_only");
        var campaign = Campaign.Current;
        if (campaign == null)
            return new CoopCommandResult(false, "Campaign is not loaded.", "campaign_unavailable");

        if (!resolver.TryResolveMobileParty(args[0], out var party, out var error))
            return new CoopCommandResult(false, error, "party_not_found");
        var partyId = resolver.DisplayId(party);
        if (!party.IsActive)
            return new CoopCommandResult(false, $"Party '{partyId}' is not active.", "party_unavailable");
        if (party.CurrentSettlement != null || party.MapEvent != null || party.SiegeEvent != null || party.Army != null)
            return new CoopCommandResult(false, "Leave the settlement, battle, siege, or army before teleporting.", "party_busy");

        var hideout = campaign.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsHideout && settlement.Hideout.IsInfested &&
                settlement.Hideout.NextPossibleAttackTime.IsPast &&
                settlement.Parties.Any(defender => defender.IsActive &&
                    (defender.IsBandit || defender.IsBanditBossParty) && defender.MemberRoster.TotalHealthyCount > 0))
            .OrderBy(settlement => party.Position.ToVec2().DistanceSquared(settlement.GatePosition.ToVec2()))
            .FirstOrDefault();
        if (hideout == null)
            return new CoopCommandResult(false, "No populated hideouts off cooldown were found in this campaign.", "hideout_not_found");
        if (!behaviorSnapshot.TryCreate(party, out var data))
            return new CoopCommandResult(false, "Unable to read the party's movement state.", "party_unavailable");

        hideout.Hideout.IsSpotted = true;
        data.PartyPosition = hideout.GatePosition;
        data.ForcePosition = true;
        data.IsCurrentlyAtSea = false;
        data.ResetMovementToHold = true;
        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
        return new CoopCommandResult(true,
            $"Requested teleport of party '{partyId}' to {hideout.Name} ({hideout.StringId}) entrance " +
            $"at {hideout.GatePosition.X:R}, {hideout.GatePosition.Y:R}.");
    }
}
