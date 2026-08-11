#if DEBUG
using Common;
using Common.Network.Coalescing;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>
/// Reversible live-test fixture for non-lord party trade-gold coalescing.
/// </summary>
internal static class PartyTradeGoldCoalescingFixtureCommands
{
    private const string Channel = "AutoSync.SetValue.MobileParty.PartyTradeGold";

    private static FixtureState fixture;
    private static PartyBase focusedParty;

    [CommandLineArgumentFunction("trade_gold_coalescing_probe", "coop.debug.mobileparty")]
    public static string Probe(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_probe <settlementId>";
        if (!TryGetServices(out var objectManager, out _, out var error))
            return error;

        Settlement anchor = Settlement.Find(args[0]);
        if (anchor == null)
            return $"Settlement '{args[0]}' was not found.";

        MobileParty party = MobileParty.All
            .Where(candidate => IsEligible(candidate) && objectManager.TryGetId(candidate, out _))
            .OrderBy(candidate => candidate.Position.DistanceSquared(anchor.Position))
            .FirstOrDefault();
        if (party == null)
            return $"No active registered non-lord mobile party is available near {anchor.StringId}.";

        objectManager.TryGetId(party, out var networkId);
        return FormatParty("probe", party, networkId, anchor);
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_setup", "coop.debug.mobileparty")]
    public static string Setup(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_setup <mobilePartyNetworkId> <settlementId>";
        if (fixture != null)
            return "A trade-gold coalescing fixture is already active.";
        if (!TryGetServices(out var objectManager, out var coalescer, out var error))
            return error;
        if (!objectManager.TryGetObject(args[0], out MobileParty party))
            return $"Mobile party '{args[0]}' was not found.";
        if (!IsEligible(party))
            return $"Mobile party '{args[0]}' is not an active non-lord campaign-map party.";

        Settlement anchor = Settlement.Find(args[1]);
        if (anchor == null)
            return $"Settlement '{args[1]}' was not found.";

        var key = new CoalesceKey(Channel, args[0]);
        if (!coalescer.TryStartDebugTrace(key))
            return "Unable to start the exact coalescer trace because another trace or pending value already exists.";

        CreateSequence(party.PartyTradeGold, out var first, out var second, out var final);
        fixture = new FixtureState(
            party,
            args[0],
            anchor,
            party.PartyTradeGold,
            first,
            second,
            final,
            coalescer);

        return FormatFixture("ready", fixture, coalescer.GetDebugTraceSnapshot());
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_trigger", "coop.debug.mobileparty")]
    public static string Trigger(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_trigger";
        if (fixture == null)
            return "The trade-gold coalescing fixture is not active.";
        if (fixture.Triggered)
            return "The trade-gold coalescing trigger has already run.";

        fixture.Triggered = true;
        fixture.Party.PartyTradeGold = fixture.First;
        fixture.Party.PartyTradeGold = fixture.Second;
        fixture.Party.PartyTradeGold = fixture.Final;

        SendCoalescer.DebugTraceSnapshot snapshot = fixture.Coalescer.GetDebugTraceSnapshot();
        if (snapshot.Enqueued != 3 || snapshot.Merged != 2 || snapshot.Sent != 0 || !snapshot.Pending)
            return "Failed: same-tick trigger did not leave exactly three enqueues merged into one pending send. " +
                FormatFixture("triggered", fixture, snapshot);

        return FormatFixture("triggered", fixture, snapshot);
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_state", "coop.debug.mobileparty")]
    public static string State(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_state";
        if (fixture == null)
            return "The trade-gold coalescing fixture is not active.";

        string phase = fixture.RestoreAttempted
            ? "restoring"
            : fixture.Triggered
                ? "triggered"
                : "ready";
        return FormatFixture(phase, fixture, fixture.Coalescer.GetDebugTraceSnapshot());
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_observe", "coop.debug.mobileparty")]
    public static string Observe(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_observe <mobilePartyNetworkId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to resolve {nameof(IObjectManager)}.";
        if (!objectManager.TryGetObject(args[0], out MobileParty party))
            return $"Mobile party '{args[0]}' was not found.";

        string side = ModInformation.IsServer ? "server" : "client";
        string output = $"side={side}|networkId={args[0]}|" +
            $"party={party.StringId}|name={Clean(party.Name?.ToString())}|" +
            $"nonLord={FormatBool(!party.IsLordParty)}|active={FormatBool(party.IsActive)}|" +
            $"gold={party.PartyTradeGold}|x={party.Position.X.ToString("R", CultureInfo.InvariantCulture)}|" +
            $"y={party.Position.Y.ToString("R", CultureInfo.InvariantCulture)}|" +
            $"settlement={party.CurrentSettlement?.StringId ?? "none"}";
        return WithStructuredResult(output, new
        {
            side,
            networkId = args[0],
            party = party.StringId,
            name = Clean(party.Name?.ToString()),
            nonLord = !party.IsLordParty,
            active = party.IsActive,
            gold = party.PartyTradeGold,
            x = party.Position.X,
            y = party.Position.Y,
            settlement = party.CurrentSettlement?.StringId ?? "none",
        });
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_focus", "coop.debug.mobileparty")]
    public static string Focus(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Command can only be run on a client.";
        if (args.Count != 1)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_focus <mobilePartyNetworkId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to resolve {nameof(IObjectManager)}.";
        if (!objectManager.TryGetObject(args[0], out MobileParty party))
            return $"Mobile party '{args[0]}' was not found.";
        if (!IsEligible(party))
            return $"Mobile party '{args[0]}' is not available on the campaign map.";

        MapScreen mapScreen = MapScreen.Instance;
        if (mapScreen == null)
            return "Campaign map screen is unavailable.";

        if (mapScreen.MapState?.AtMenu == true)
            mapScreen.MapState.ExitMenuMode();

        party.Party.SetAsCameraFollowParty();
        mapScreen.MapCameraView.ResetCamera(resetDistance: true, teleportToMainParty: false);
        focusedParty = party.Party;
        RefreshFocusedPartyTooltip(mapScreen);
        string output = $"focused=true|networkId={args[0]}|party={party.StringId}|" +
            $"name={Clean(party.Name?.ToString())}|gold={party.PartyTradeGold}";
        return WithStructuredResult(output, new
        {
            focused = true,
            networkId = args[0],
            party = party.StringId,
            name = Clean(party.Name?.ToString()),
            gold = party.PartyTradeGold,
        });
    }

    internal static void RefreshFocusedPartyTooltip(MapScreen mapScreen)
    {
        if (mapScreen == null || focusedParty?.MobileParty == null || !focusedParty.MobileParty.IsActive)
        {
            focusedParty = null;
            return;
        }

        var partyVisual = focusedParty.GetPartyVisual();
        if (partyVisual == null || mapScreen.CurrentVisualOfTooltip == partyVisual)
            return;

        mapScreen.RemoveMapTooltip();
        mapScreen.OnHoverMapEntity(partyVisual);
        mapScreen.CurrentVisualOfTooltip = partyVisual;
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_restore", "coop.debug.mobileparty")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_restore";
        if (fixture == null)
            return "The trade-gold coalescing fixture is not active.";
        if (!fixture.Triggered)
            return "The trade-gold coalescing trigger has not run; cancel the fixture instead.";
        if (fixture.RestoreAttempted)
            return "The trade-gold coalescing restoration has already been attempted.";

        fixture.RestoreAttempted = true;
        fixture.Party.PartyTradeGold = fixture.Original;
        return FormatFixture("restoring", fixture, fixture.Coalescer.GetDebugTraceSnapshot());
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_finish", "coop.debug.mobileparty")]
    public static string Finish(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_finish";
        if (fixture == null)
            return "The trade-gold coalescing fixture is not active.";

        SendCoalescer.DebugTraceSnapshot snapshot = fixture.Coalescer.GetDebugTraceSnapshot();
        if (!fixture.RestoreAttempted || fixture.Party.PartyTradeGold != fixture.Original ||
            snapshot.Pending || snapshot.Enqueued != 4 || snapshot.Merged != 2 || snapshot.Sent != 2)
        {
            return "Restoration is not complete. " + FormatFixture("restoring", fixture, snapshot);
        }

        string result = FormatFixture("restored", fixture, snapshot, verified: true);
        fixture.Coalescer.StopDebugTrace();
        fixture = null;
        return result;
    }

    [CommandLineArgumentFunction("trade_gold_coalescing_cancel", "coop.debug.mobileparty")]
    public static string Cancel(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.trade_gold_coalescing_cancel";
        if (fixture == null)
            return "The trade-gold coalescing fixture is not active.";
        if (fixture.Triggered)
            return "The trigger already ran; restore and finish the fixture instead.";

        fixture.Coalescer.StopDebugTrace();
        fixture = null;
        return "Trade-gold coalescing fixture cancelled before mutation.";
    }

    private static bool TryGetServices(
        out IObjectManager objectManager,
        out SendCoalescer coalescer,
        out string error)
    {
        objectManager = null;
        coalescer = null;
        error = null;

        if (!ContainerProvider.TryResolve(out objectManager))
        {
            error = $"Unable to resolve {nameof(IObjectManager)}.";
            return false;
        }
        if (!ContainerProvider.TryResolve<ISendCoalescer>(out var sendCoalescer) ||
            sendCoalescer is not SendCoalescer concreteCoalescer)
        {
            error = $"Unable to resolve {nameof(SendCoalescer)}.";
            return false;
        }

        coalescer = concreteCoalescer;
        return true;
    }

    private static bool IsEligible(MobileParty party) =>
        party?.Party != null &&
        party.IsActive &&
        !party.IsLordParty &&
        !party.IsPlayerParty() &&
        party.CurrentSettlement == null &&
        party.MapEvent == null &&
        party.Army == null &&
        !party.IsTransitionInProgress;

    private static void CreateSequence(int original, out int first, out int second, out int final)
    {
        int direction = original <= int.MaxValue - 303 ? 1 : -1;
        first = original + (101 * direction);
        second = original + (202 * direction);
        final = original + (303 * direction);
    }

    private static string FormatParty(
        string phase,
        MobileParty party,
        string networkId,
        Settlement anchor)
    {
        string output = FormatPartyText(phase, party, networkId, anchor);
        return WithStructuredResult(output, new
        {
            phase,
            networkId,
            party = party.StringId,
            name = Clean(party.Name?.ToString()),
            anchor = anchor.StringId,
            distance = party.Position.Distance(anchor.Position),
            nonLord = !party.IsLordParty,
            registered = true,
            active = party.IsActive,
            gold = party.PartyTradeGold,
            x = party.Position.X,
            y = party.Position.Y,
        });
    }

    private static string FormatFixture(
        string phase,
        FixtureState state,
        SendCoalescer.DebugTraceSnapshot snapshot,
        bool verified = false)
    {
        string output = FormatPartyText(phase, state.Party, state.NetworkId, state.Anchor) +
            $"|original={state.Original}|first={state.First}|second={state.Second}|final={state.Final}|" +
            $"current={state.Party.PartyTradeGold}|enqueued={snapshot.Enqueued}|" +
            $"merged={snapshot.Merged}|sent={snapshot.Sent}|pending={FormatBool(snapshot.Pending)}|" +
            $"triggered={FormatBool(state.Triggered)}|restoreAttempted={FormatBool(state.RestoreAttempted)}" +
            (verified ? "|verified=true" : string.Empty);
        return WithStructuredResult(output, new
        {
            phase,
            networkId = state.NetworkId,
            party = state.Party.StringId,
            name = Clean(state.Party.Name?.ToString()),
            anchor = state.Anchor.StringId,
            distance = state.Party.Position.Distance(state.Anchor.Position),
            nonLord = !state.Party.IsLordParty,
            registered = true,
            active = state.Party.IsActive,
            gold = state.Party.PartyTradeGold,
            x = state.Party.Position.X,
            y = state.Party.Position.Y,
            original = state.Original,
            first = state.First,
            second = state.Second,
            final = state.Final,
            current = state.Party.PartyTradeGold,
            enqueued = snapshot.Enqueued,
            merged = snapshot.Merged,
            sent = snapshot.Sent,
            pending = snapshot.Pending,
            triggered = state.Triggered,
            restoreAttempted = state.RestoreAttempted,
            verified,
        });
    }

    private static string FormatPartyText(
        string phase,
        MobileParty party,
        string networkId,
        Settlement anchor) =>
        $"phase={phase}|networkId={networkId}|party={party.StringId}|" +
        $"name={Clean(party.Name?.ToString())}|anchor={anchor.StringId}|" +
        $"distance={party.Position.Distance(anchor.Position).ToString("R", CultureInfo.InvariantCulture)}|" +
        $"nonLord={FormatBool(!party.IsLordParty)}|registered=true|active={FormatBool(party.IsActive)}|" +
        $"gold={party.PartyTradeGold}|x={party.Position.X.ToString("R", CultureInfo.InvariantCulture)}|" +
        $"y={party.Position.Y.ToString("R", CultureInfo.InvariantCulture)}";

    private static string WithStructuredResult(string output, object structuredResult) =>
        output + Environment.NewLine + "LIVE_TEST_JSON=" + JsonSerializer.Serialize(structuredResult);

    private static string Clean(string value) =>
        string.IsNullOrEmpty(value) ? "unnamed" : value.Replace('|', '/');

    private static string FormatBool(bool value) => value ? "true" : "false";

    private sealed class FixtureState
    {
        public MobileParty Party { get; }
        public string NetworkId { get; }
        public Settlement Anchor { get; }
        public int Original { get; }
        public int First { get; }
        public int Second { get; }
        public int Final { get; }
        public SendCoalescer Coalescer { get; }
        public bool Triggered { get; set; }
        public bool RestoreAttempted { get; set; }

        public FixtureState(
            MobileParty party,
            string networkId,
            Settlement anchor,
            int original,
            int first,
            int second,
            int final,
            SendCoalescer coalescer)
        {
            Party = party;
            NetworkId = networkId;
            Anchor = anchor;
            Original = original;
            First = first;
            Second = second;
            Final = final;
            Coalescer = coalescer;
        }
    }
}

[HarmonyPatch(typeof(MapScreen), nameof(MapScreen.HandleMouse))]
internal static class PartyTradeGoldCoalescingTooltipPatch
{
    private static void Postfix(MapScreen __instance)
    {
        PartyTradeGoldCoalescingFixtureCommands.RefreshFocusedPartyTooltip(__instance);
    }
}
#endif
