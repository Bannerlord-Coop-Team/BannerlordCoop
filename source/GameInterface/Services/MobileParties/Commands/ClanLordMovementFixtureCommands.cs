#if DEBUG
using Common.Commands;
using System;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>Exposes fixture setup, production actions, observations, and restoration.</summary>
internal static class ClanLordMovementFixtureCommands
{
    private static readonly IExpectedArgs[] ObservationArgs = new IExpectedArgs[]
    {
        new ExpectedArgs("phase", "before, during, released, verify, or state."),
        new ExpectedArgs("playerPartyId", "Exact player MobileParty registry id from setup."),
        new ExpectedArgs("lordPartyId", "Exact lord MobileParty registry id from setup."),
        new ExpectedArgs("interactionPartyId", "Exact interaction MobileParty registry id from setup."),
        new ExpectedArgs("targetX", "Target X returned by setup."),
        new ExpectedArgs("targetY", "Target Y returned by setup."),
        new ExpectedArgs("baselineX", "Baseline X returned by the released observation on this machine."),
        new ExpectedArgs("baselineY", "Baseline Y returned by the released observation on this machine."),
        new ExpectedArgs("baselineTicks", "Baseline ticks returned by the released observation on this machine."),
        new ExpectedArgs("token", "Fixture token returned by setup."),
    };

    /// <summary>Stages a registered clan lord and interaction party through authoritative server mutations.</summary>
    public sealed class Setup : ICoopCommand
    {
        private readonly IClanLordMovementFixture fixture;
        public Setup(IClanLordMovementFixture fixture) => this.fixture = fixture;
        public string Prefix => "coop.debug.mobileparty";
        public string Name => "clan_lord_fixture_setup";
        public string Description => "Stage a real clan lord and nearby interaction party for issue 3264.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("playerPartyId", "Exact registered MobileParty id of the participating player.") };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Setup(args[0]);
    }

    /// <summary>Records local conversation evidence and checks post-release movement.</summary>
    public sealed class Observe : ICoopCommand
    {
        private readonly IClanLordMovementFixture fixture;
        public Observe(IClanLordMovementFixture fixture) => this.fixture = fixture;
        public string Prefix => "coop.debug.mobileparty";
        public string Name => "clan_lord_fixture_observe";
        public string Description => "Read before/during/released/verify evidence for issue 3264; never changes game state.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs => ObservationArgs;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Observe(args);
    }

    /// <summary>Starts the fixture conversation through the production encounter action.</summary>
    public sealed class Start : ICoopCommand
    {
        private readonly IClanLordMovementFixture fixture;
        public Start(IClanLordMovementFixture fixture) => this.fixture = fixture;
        public string Prefix => "coop.debug.mobileparty";
        public string Name => "clan_lord_fixture_start";
        public string Description => "Start the issue 3264 fixture through the production conversation action.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs => ObservationArgs;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Start(args);
    }

    /// <summary>Finishes the exact fixture conversation through the production encounter action.</summary>
    public sealed class Finish : ICoopCommand
    {
        private readonly IClanLordMovementFixture fixture;
        public Finish(IClanLordMovementFixture fixture) => this.fixture = fixture;
        public string Prefix => "coop.debug.mobileparty";
        public string Name => "clan_lord_fixture_finish";
        public string Description => "Finish the issue 3264 fixture through the production conversation action.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs => ObservationArgs;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Finish(args);
    }

    /// <summary>Retries restoration of the original captured party states.</summary>
    public sealed class Restore : ICoopCommand
    {
        private readonly IClanLordMovementFixture fixture;
        public Restore(IClanLordMovementFixture fixture) => this.fixture = fixture;
        public string Prefix => "coop.debug.mobileparty";
        public string Name => "clan_lord_fixture_restore";
        public string Description => "Restore the captured lord and interaction party state, retrying partial restoration.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Restore();
    }
}
#endif
