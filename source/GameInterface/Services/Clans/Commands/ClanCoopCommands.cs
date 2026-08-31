using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Clans.Commands;

public interface IClanOpenCommand : ICoopCommand
{
}

public sealed class ClanOpenCommand : LegacyCoopCommand, IClanOpenCommand
{
    public ClanOpenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "open",
            "Opens the clan screen on a client.",
            System.Array.Empty<IExpectedArgs>(),
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.OpenClanScreen)
    {
    }
}

public interface IClanCloseCommand : ICoopCommand
{
}

public sealed class ClanCloseCommand : LegacyCoopCommand, IClanCloseCommand
{
    public ClanCloseCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "close",
            "Closes the clan screen on a client.",
            System.Array.Empty<IExpectedArgs>(),
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.CloseClanScreen)
    {
    }
}

public interface IClanScreenStateCommand : ICoopCommand
{
}

public sealed class ClanScreenStateCommand : LegacyCoopCommand, IClanScreenStateCommand
{
    public ClanScreenStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "screen_state",
            "Reports clan screen state.",
            System.Array.Empty<IExpectedArgs>(),
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ClanScreenState)
    {
    }
}

public interface IClanSelectPartiesCommand : ICoopCommand
{
}

public sealed class ClanSelectPartiesCommand : LegacyCoopCommand, IClanSelectPartiesCommand
{
    public ClanSelectPartiesCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "select_parties",
            "Selects the parties tab on the clan screen.",
            System.Array.Empty<IExpectedArgs>(),
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.SelectParties)
    {
    }
}

public interface IClanWageStateCommand : ICoopCommand
{
}

public sealed class ClanWageStateCommand : LegacyCoopCommand, IClanWageStateCommand
{
    public ClanWageStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "wage_state",
            "Reports clan party wage state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The optional registered clan id.", false),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.WageState)
    {
    }
}

public interface IClanRefreshBurstCommand : ICoopCommand
{
}

public sealed class ClanRefreshBurstCommand : LegacyCoopCommand, IClanRefreshBurstCommand
{
    public ClanRefreshBurstCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "refresh_burst",
            "Sends repeated party role refresh messages.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("partyId", "The registered mobile party id."),
                new ExpectedArgs("count", "A message count from 1 through 500."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.RefreshBurst)
    {
    }
}

public interface IClanListCommand : ICoopCommand
{
}

public sealed class ClanListCommand : LegacyCoopCommand, IClanListCommand
{
    public ClanListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "list",
            "Lists campaign clans.",
            System.Array.Empty<IExpectedArgs>(),
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ListClans)
    {
    }
}

public interface IClanFieldDumpCommand : ICoopCommand
{
}

public sealed class ClanFieldDumpCommand : LegacyCoopCommand, IClanFieldDumpCommand
{
    public ClanFieldDumpCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "field_dump",
            "Dumps every field of a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.Info)
    {
    }
}

public interface IClanAddInfluenceCommand : ICoopCommand
{
}

public sealed class ClanAddInfluenceCommand : LegacyCoopCommand, IClanAddInfluenceCommand
{
    public ClanAddInfluenceCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "add_influence",
            "Adds influence to a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("amount", "The numeric influence amount."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.AddClanInfluence)
    {
    }
}

public interface IClanChangeLeaderCommand : ICoopCommand
{
}

public sealed class ClanChangeLeaderCommand : LegacyCoopCommand, IClanChangeLeaderCommand
{
    public ClanChangeLeaderCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "change_clan_leader",
            "Changes the leader of a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("heroId", "The registered new leader hero id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ChangeClanLeader)
    {
    }
}

public interface IClanChangeKingdomCommand : ICoopCommand
{
}

public sealed class ClanChangeKingdomCommand : LegacyCoopCommand, IClanChangeKingdomCommand
{
    public ClanChangeKingdomCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "change_clan_kingdom",
            "Moves a registered clan to a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ChangeClanKingdom)
    {
    }
}

public interface IClanDestroyCommand : ICoopCommand
{
}

public sealed class ClanDestroyCommand : LegacyCoopCommand, IClanDestroyCommand
{
    public ClanDestroyCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "destroy_clan",
            "Destroys a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.DestroyClan)
    {
    }
}

public interface IClanAddCompanionCommand : ICoopCommand
{
}

public sealed class ClanAddCompanionCommand : LegacyCoopCommand, IClanAddCompanionCommand
{
    public ClanAddCompanionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "add_companion",
            "Adds a registered companion to a clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("heroId", "The registered companion hero id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.AddCompanion)
    {
    }
}

public interface IClanRemoveCompanionCommand : ICoopCommand
{
}

public sealed class ClanRemoveCompanionCommand : LegacyCoopCommand, IClanRemoveCompanionCommand
{
    public ClanRemoveCompanionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "remove_companion",
            "Removes a registered companion from a clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered companion hero id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.RemoveCompanion)
    {
    }
}

public interface IClanAddRenownCommand : ICoopCommand
{
}

public sealed class ClanAddRenownCommand : LegacyCoopCommand, IClanAddRenownCommand
{
    public ClanAddRenownCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "add_renown",
            "Adds renown to a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("renown", "The integer renown amount."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.AddRenown)
    {
    }
}

public interface IClanEconomyCommand : ICoopCommand
{
}

public sealed class ClanEconomyCommand : LegacyCoopCommand, IClanEconomyCommand
{
    public ClanEconomyCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "economy",
            "Reports battle-economy values for a clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanIdOrName", "The optional clan id or display name. Quote multi-word names.", false),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ClanEconomy)
    {
    }
}

public interface IClanJoinKingdomCommand : ICoopCommand
{
}

public sealed class ClanJoinKingdomCommand : LegacyCoopCommand, IClanJoinKingdomCommand
{
    public ClanJoinKingdomCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "join_kingdom",
            "Joins a registered clan to a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.JoinKingdom)
    {
    }
}

public interface IClanLeaveKingdomCommand : ICoopCommand
{
}

public sealed class ClanLeaveKingdomCommand : LegacyCoopCommand, IClanLeaveKingdomCommand
{
    public ClanLeaveKingdomCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "leave_kingdom",
            "Removes a registered clan from its kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.LeaveKingdom)
    {
    }
}

public interface IClanMembershipCommand : ICoopCommand
{
}

public sealed class ClanMembershipCommand : LegacyCoopCommand, IClanMembershipCommand
{
    public ClanMembershipCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "membership",
            "Reports kingdom membership for a clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.Membership)
    {
    }
}

public interface IClanGiveInfluenceCommand : ICoopCommand
{
}

public sealed class ClanGiveInfluenceCommand : LegacyCoopCommand, IClanGiveInfluenceCommand
{
    public ClanGiveInfluenceCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "give_influence",
            "Gives influence to a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
                new ExpectedArgs("amount", "The numeric influence amount."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.GiveInfluence)
    {
    }
}

public interface IClanInfoCommand : ICoopCommand
{
}

public sealed class ClanInfoCommand : LegacyCoopCommand, IClanInfoCommand
{
    public ClanInfoCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "info",
            "Reports a curated summary for a registered clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.InfoClan)
    {
    }
}

public interface IClanDailyGoldChangeCommand : ICoopCommand
{
}

public sealed class ClanDailyGoldChangeCommand : LegacyCoopCommand, IClanDailyGoldChangeCommand
{
    public ClanDailyGoldChangeCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.clan",
            "daily_gold_change",
            "Reports predicted daily gold changes for a clan.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("clanId", "The registered clan id."),
            },
            global::GameInterface.Services.GameDebug.Commands.ClanDebugCommands.ViewPredicatedDailyGoldChange)
    {
    }
}
