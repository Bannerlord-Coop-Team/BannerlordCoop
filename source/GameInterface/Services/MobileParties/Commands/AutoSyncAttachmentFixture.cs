#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Commands;

public interface IAutoSyncAttachmentFixture
{
    CoopCommandResult Candidates();
    CoopCommandResult Execute(ICoopCommandArgs args, bool mutate);
}

public sealed class AutoSyncAttachmentFixture : IAutoSyncAttachmentFixture
{
    private readonly IObjectManager objectManager;
    private readonly ITimeControlInterface timeControl;

    public AutoSyncAttachmentFixture(IObjectManager objectManager, ITimeControlInterface timeControl)
    {
        this.objectManager = objectManager;
        this.timeControl = timeControl;
    }

    public CoopCommandResult Candidates()
    {
        if (Campaign.Current == null)
            return Failure("Load a disposable campaign first.");

        var ids = Campaign.Current.CampaignObjectManager.MobileParties
            .Where(party => IsIdleOnLand(party) && party.AttachedTo == null && party.AttachedParties.Count == 0)
            .OrderBy(party => party.StringId, StringComparer.Ordinal)
            .Select(party => objectManager.TryGetId(party, out var id) ? id : null)
            .Where(id => id != null)
            .Take(2)
            .ToArray();
        if (ids.Length != 2)
            return Failure("No eligible registered party pair; keep this path unexercised.");

        return new CoopCommandResult(true,
            $"Read-only selection: child={ids[0]}, parent={ids[1]}\n" +
            $"All machines: coop.debug.autosync.attachment_state {ids[0]} {ids[1]}\n" +
            $"Server: coop.debug.autosync.attachment {ids[0]} {ids[1]} attach\n" +
            $"Server: coop.debug.autosync.attachment {ids[0]} {ids[1]} detach");
    }

    public CoopCommandResult Execute(ICoopCommandArgs args, bool mutate)
    {
        if (mutate && ModInformation.IsClient)
            return Failure("Run attachment on the server.");
        if (args.Count != (mutate ? 3 : 2))
            return Failure("Expected child_id parent_id, then attach or detach for attachment.");
        if (mutate && args[2] != "attach" && args[2] != "detach")
            return Failure("Action must be attach or detach.");
        if (mutate && timeControl.GetTimeControl() != TimeControlEnum.Pause)
            return Failure("Pause the campaign before changing attachment.");
        if (!objectManager.TryGetObject<MobileParty>(args[0], out var child) ||
            !objectManager.TryGetObject<MobileParty>(args[1], out var parent))
            return Failure("Both party ids must be registered on this machine.");
        if (ReferenceEquals(child, parent))
            return Failure("Choose two different parties.");

        if (mutate)
        {
            if (!IsIdleOnLand(child) || !IsIdleOnLand(parent) ||
                child.AttachedParties.Count != 0 || parent.AttachedTo != null ||
                (child.AttachedTo != null && child.AttachedTo != parent))
                return Failure("Use active idle land parties with no army, settlement, battle, siege, transition, or other attachments.");

            int expectedCount = child.AttachedTo == parent ? 1 : 0;
            if (parent.AttachedParties.Count != expectedCount ||
                parent.AttachedParties.Count(party => ReferenceEquals(party, child)) != expectedCount)
                return Failure("Attachment graph is inconsistent or contains another party; reload the disposable save.");

            // Keep authoritative patches active so the property publishes its normal network message.
            var target = args[2] == "attach" ? parent : null;
            if (child.AttachedTo != target)
                child.AttachedTo = target;
        }

        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            role = ModInformation.IsServer ? "server" : "client",
            timeMode = timeControl.GetTimeControl().ToString(),
            childId = args[0],
            parentId = args[1],
            attachedToParent = ReferenceEquals(child.AttachedTo, parent),
            childDetached = child.AttachedTo == null,
            parentChildCount = parent.AttachedParties.Count(party => ReferenceEquals(party, child)),
            parentAttachmentCount = parent.AttachedParties.Count,
            childAttachmentCount = child.AttachedParties.Count,
            parentDetached = parent.AttachedTo == null,
            childIdleOnLand = IsIdleOnLand(child),
            parentIdleOnLand = IsIdleOnLand(parent)
        }));
    }

    private static bool IsIdleOnLand(MobileParty party) =>
        party.IsActive && !party.IsCurrentlyAtSea && !party.IsTransitionInProgress &&
        party.Army == null && party.CurrentSettlement == null &&
        party.MapEvent == null && party.Party.MapEventSide == null && party.BesiegerCamp == null;

    private static CoopCommandResult Failure(string message) =>
        new CoopCommandResult(false, message, "fixture_precondition_failed");
}

public sealed class AutoSyncAttachmentCommand : ICoopCommand
{
    private readonly IAutoSyncAttachmentFixture fixture;

    public AutoSyncAttachmentCommand(IAutoSyncAttachmentFixture fixture) => this.fixture = fixture;

    public string Prefix => "coop.debug.autosync";
    public string Name => "attachment";
    public string Description => "Attaches or detaches two idle parties for the AutoSync fixture.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("child_id", "Registered child party id."),
        new ExpectedArgs("parent_id", "Registered parent party id."),
        new ExpectedArgs("action", "attach or detach.")
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Execute(args, true);
}

public sealed class AutoSyncAttachmentStateCommand : ICoopCommand
{
    private readonly IAutoSyncAttachmentFixture fixture;

    public AutoSyncAttachmentStateCommand(IAutoSyncAttachmentFixture fixture) => this.fixture = fixture;

    public string Prefix => "coop.debug.autosync";
    public string Name => "attachment_state";
    public string Description => "Reports both sides of a party attachment without changing them.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("child_id", "Registered child party id."),
        new ExpectedArgs("parent_id", "Registered parent party id.")
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Execute(args, false);
}

public sealed class AutoSyncAttachmentCandidatesCommand : ICoopCommand
{
    private readonly IAutoSyncAttachmentFixture fixture;

    public AutoSyncAttachmentCandidatesCommand(IAutoSyncAttachmentFixture fixture) => this.fixture = fixture;

    public string Prefix => "coop.debug.autosync";
    public string Name => "attachment_candidates";
    public string Description => "Prints runnable fixture commands for two registered idle land parties.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Candidates();
}
#endif
