#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Commands;

public sealed class PartyMovementFixtureCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IMobilePartyBehaviorSnapshot snapshots;
    private readonly IMessageBroker messageBroker;
    private readonly Dictionary<string, (PartyBehaviorUpdateData Behavior, string Settlement)> captured = new();

    public PartyMovementFixtureCommand(IObjectManager objectManager, IMobilePartyBehaviorSnapshot snapshots, IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.snapshots = snapshots;
        this.messageBroker = messageBroker;
    }

    public string Prefix => "coop.debug.mobile_party";
    public string Name => "movement_fixture";
    public string Description => "Captures or restores movement after restoring a fixture party's settlement and position.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("operation", "capture or restore"),
        new ExpectedArgs("party_id", "The registered mobile-party id."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsServer) return new CoopCommandResult(false, "Run on the server.", "command_failed");
        if (args[0] != "capture" && args[0] != "restore") return new CoopCommandResult(false, "Use capture or restore.", "command_failed");
        if (!objectManager.TryGetObject(args[1], out MobileParty party) || party.MapEvent != null)
            return new CoopCommandResult(false, "An idle registered party is required.", "command_failed");
        if (args[0] == "capture")
        {
            if (captured.ContainsKey(args[1])) return new CoopCommandResult(false, "Movement already captured.", "command_failed");
            if (!snapshots.TryCreate(party, out var behavior)) return new CoopCommandResult(false, "Movement capture failed.", "command_failed");
            captured.Add(args[1], (behavior, party.CurrentSettlement?.StringId));
            return new CoopCommandResult(true, "Movement captured.");
        }

        if (!captured.TryGetValue(args[1], out var original)) return new CoopCommandResult(false, "No captured movement.", "command_failed");
        if (party.CurrentSettlement?.StringId != original.Settlement ||
            party.Position.X != original.Behavior.PartyPosition.X || party.Position.Y != original.Behavior.PartyPosition.Y ||
            party.Position.IsOnLand != original.Behavior.PartyPosition.IsOnLand)
            return new CoopCommandResult(false, "Restore the original settlement and position first.", "command_failed");
        if (!snapshots.TryApply(party, original.Behavior, out _)) return new CoopCommandResult(false, "Movement restoration failed.", "command_failed");
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));
        captured.Remove(args[1]);
        return new CoopCommandResult(true, "Movement restored.");
    }
}
#endif
