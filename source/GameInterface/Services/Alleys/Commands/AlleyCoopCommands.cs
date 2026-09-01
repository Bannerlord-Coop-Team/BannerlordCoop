using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Alleys.Commands;

public interface IAlleyListCommand : ICoopCommand
{
}

public sealed class AlleyListCommand : LegacyCoopCommand, IAlleyListCommand
{
    public AlleyListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "list",
            "Lists alleys in a settlement.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
            },
            AlleyDebugCommand.List)
    {
    }
}

public interface IAlleyMyHeroIdCommand : ICoopCommand
{
}

public sealed class AlleyMyHeroIdCommand : LegacyCoopCommand, IAlleyMyHeroIdCommand
{
    public AlleyMyHeroIdCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "my_hero_id",
            "Reports the local main hero registry id.",
            System.Array.Empty<IExpectedArgs>(),
            AlleyDebugCommand.MyHeroId)
    {
    }
}

public interface IAlleySetOwnerCommand : ICoopCommand
{
}

public sealed class AlleySetOwnerCommand : LegacyCoopCommand, IAlleySetOwnerCommand
{
    public AlleySetOwnerCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "set_owner",
            "Sets an alley owner on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
                new ExpectedArgs("heroRegistryId", "The registered owner hero id."),
            },
            AlleyDebugCommand.SetOwner)
    {
    }
}

public interface IAlleyAbandonCommand : ICoopCommand
{
}

public sealed class AlleyAbandonCommand : LegacyCoopCommand, IAlleyAbandonCommand
{
    public AlleyAbandonCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "abandon",
            "Abandons a player-owned alley on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
            },
            AlleyDebugCommand.Abandon)
    {
    }
}

public interface IAlleyDailyTickCommand : ICoopCommand
{
}

public sealed class AlleyDailyTickCommand : LegacyCoopCommand, IAlleyDailyTickCommand
{
    public AlleyDailyTickCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "daily_tick",
            "Runs one authoritative alley daily tick.",
            System.Array.Empty<IExpectedArgs>(),
            AlleyDebugCommand.DailyTick)
    {
    }
}

public interface IAlleyAttackCommand : ICoopCommand
{
}

public sealed class AlleyAttackCommand : LegacyCoopCommand, IAlleyAttackCommand
{
    public AlleyAttackCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "attack",
            "Starts an AI attack against a player-owned alley.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
            },
            AlleyDebugCommand.Attack)
    {
    }
}

public interface IAlleyInfoCommand : ICoopCommand
{
}

public sealed class AlleyInfoCommand : LegacyCoopCommand, IAlleyInfoCommand
{
    public AlleyInfoCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "info",
            "Reports state for an alley.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
            },
            AlleyDebugCommand.Info)
    {
    }
}

public interface IAlleyRecruitFixtureStartCommand : ICoopCommand
{
}

public sealed class AlleyRecruitFixtureStartCommand : LegacyCoopCommand, IAlleyRecruitFixtureStartCommand
{
    public AlleyRecruitFixtureStartCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_fixture_start",
            "Starts the alley recruitment fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
                new ExpectedArgs("heroRegistryId", "The registered owner hero id."),
            },
            AlleyRecruitDebugCommand.StartFixture)
    {
    }
}

public interface IAlleyRecruitFixtureStateCommand : ICoopCommand
{
}

public sealed class AlleyRecruitFixtureStateCommand : LegacyCoopCommand, IAlleyRecruitFixtureStateCommand
{
    public AlleyRecruitFixtureStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_fixture_state",
            "Reports alley recruitment fixture state.",
            System.Array.Empty<IExpectedArgs>(),
            AlleyRecruitDebugCommand.FixtureState)
    {
    }
}

public interface IAlleyRecruitRosterCommand : ICoopCommand
{
}

public sealed class AlleyRecruitRosterCommand : LegacyCoopCommand, IAlleyRecruitRosterCommand
{
    public AlleyRecruitRosterCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_roster",
            "Reports the recruit roster for a hero party.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroRegistryId", "The registered hero id."),
            },
            AlleyRecruitDebugCommand.RecruitRoster)
    {
    }
}

public interface IAlleyRecruitFixtureRestoreCommand : ICoopCommand
{
}

public sealed class AlleyRecruitFixtureRestoreCommand : LegacyCoopCommand, IAlleyRecruitFixtureRestoreCommand
{
    public AlleyRecruitFixtureRestoreCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_fixture_restore",
            "Restores the alley recruitment fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("characterId", "An optional extra character id to reset.", false),
            },
            AlleyRecruitDebugCommand.RestoreFixture)
    {
    }
}

public interface IAlleyRecruitOverseerStateCommand : ICoopCommand
{
}

public sealed class AlleyRecruitOverseerStateCommand : LegacyCoopCommand, IAlleyRecruitOverseerStateCommand
{
    public AlleyRecruitOverseerStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_overseer_state",
            "Reports the alley overseer mission state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
            },
            AlleyRecruitDebugCommand.RecruitOverseerState)
    {
    }
}

public interface IAlleyRecruitConversationStartCommand : ICoopCommand
{
}

public sealed class AlleyRecruitConversationStartCommand : LegacyCoopCommand, IAlleyRecruitConversationStartCommand
{
    public AlleyRecruitConversationStartCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_conversation_start",
            "Starts a conversation with the alley overseer.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The settlement StringId."),
                new ExpectedArgs("alleyIndex", "The zero-based alley index."),
            },
            AlleyRecruitDebugCommand.StartRecruitConversation)
    {
    }
}

public interface IAlleyRecruitConversationCommand : ICoopCommand
{
}

public sealed class AlleyRecruitConversationCommand : LegacyCoopCommand, IAlleyRecruitConversationCommand
{
    public AlleyRecruitConversationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_conversation",
            "Drives or inspects the alley recruitment conversation.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("action", "One of state, ask, or accept."),
            },
            AlleyRecruitDebugCommand.RecruitConversation)
    {
    }
}

public interface IAlleyRecruitInventoryCommand : ICoopCommand
{
}

public sealed class AlleyRecruitInventoryCommand : LegacyCoopCommand, IAlleyRecruitInventoryCommand
{
    public AlleyRecruitInventoryCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_inventory",
            "Drives or inspects the alley recruitment inventory screen.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("action", "One of open, trade, complete, or state."),
            },
            AlleyRecruitDebugCommand.RecruitInventory)
    {
    }
}

public interface IAlleyRecruitStartLooterBattleCommand : ICoopCommand
{
}

public sealed class AlleyRecruitStartLooterBattleCommand : LegacyCoopCommand, IAlleyRecruitStartLooterBattleCommand
{
    public AlleyRecruitStartLooterBattleCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.alley",
            "recruit_start_looter_battle",
            "Starts the alley recruitment looter battle fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroRegistryId", "The registered fixture-owner hero id."),
            },
            AlleyRecruitDebugCommand.StartLooterBattle)
    {
    }
}
