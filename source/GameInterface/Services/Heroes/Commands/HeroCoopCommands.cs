using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Heroes.Commands;

public interface IHeroBoostFighterCommand : ICoopCommand
{
}

public sealed class HeroBoostFighterCommand : LegacyCoopCommand, IHeroBoostFighterCommand
{
    public HeroBoostFighterCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "boost_fighter",
            "Boosts a registered hero for fighter fixture testing.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroRegistryId", "The registered hero id to boost."),
            },
            HeroBoostFighterDebugCommand.BoostFighter)
    {
    }
}

#if DEBUG
public interface IHeroConversationOpenCommand : ICoopCommand
{
}

public sealed class HeroConversationOpenCommand : LegacyCoopCommand, IHeroConversationOpenCommand
{
    public HeroConversationOpenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero_conversation",
            "open",
            "Opens a conversation with a registered hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id to converse with."),
            },
            HeroConversationDebugCommand.Open)
    {
    }
}

public interface IHeroConversationStateCommand : ICoopCommand
{
}

public sealed class HeroConversationStateCommand : LegacyCoopCommand, IHeroConversationStateCommand
{
    public HeroConversationStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero_conversation",
            "state",
            "Reports the current hero conversation state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The optional registered hero id to compare with the active conversation.", false),
            },
            HeroConversationDebugCommand.State)
    {
    }
}

public interface IHeroConversationCloseCommand : ICoopCommand
{
}

public sealed class HeroConversationCloseCommand : LegacyCoopCommand, IHeroConversationCloseCommand
{
    public HeroConversationCloseCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero_conversation",
            "close",
            "Closes the active hero conversation.",
            System.Array.Empty<IExpectedArgs>(),
            HeroConversationDebugCommand.Close)
    {
    }
}

public interface IHeroConversationSetHasMetCommand : ICoopCommand
{
}

public sealed class HeroConversationSetHasMetCommand : LegacyCoopCommand, IHeroConversationSetHasMetCommand
{
    public HeroConversationSetHasMetCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero_conversation",
            "set_has_met",
            "Sets whether the local player has met a hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("hasMet", "True when the hero should be marked as met."),
            },
            HeroConversationDebugCommand.SetHasMet)
    {
    }
}

public interface IHeroConversationMeetingStateCommand : ICoopCommand
{
}

public sealed class HeroConversationMeetingStateCommand : LegacyCoopCommand, IHeroConversationMeetingStateCommand
{
    public HeroConversationMeetingStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero_conversation",
            "meeting_state",
            "Reports cached meeting state for two heroes.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("metHeroId", "The registered met hero id."),
            },
            HeroConversationDebugCommand.MeetingState)
    {
    }
}

#endif

public interface IHeroListCommand : ICoopCommand
{
}

public sealed class HeroListCommand : LegacyCoopCommand, IHeroListCommand
{
    public HeroListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "list",
            "Lists registered heroes, optionally filtered by display-name prefix.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("namePrefix", "The optional display-name prefix. Quote multi-word values.", false),
            },
            HeroDebugCommand.ListHeroes)
    {
    }
}

public interface IHeroHomeSettlementSnapshotCommand : ICoopCommand
{
}

public sealed class HeroHomeSettlementSnapshotCommand : LegacyCoopCommand, IHeroHomeSettlementSnapshotCommand
{
    public HeroHomeSettlementSnapshotCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "home_settlement_snapshot",
            "Reports registered hero home-settlement state.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("resolveMissing", "Whether missing home settlements should be resolved."),
            },
            HeroDebugCommand.HomeSettlementSnapshot)
    {
    }
}

public interface IHeroInfoCommand : ICoopCommand
{
}

public sealed class HeroInfoCommand : LegacyCoopCommand, IHeroInfoCommand
{
    public HeroInfoCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "info",
            "Dumps fields for a registered hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
            },
            HeroDebugCommand.Info)
    {
    }
}

public interface IHeroCreateCommand : ICoopCommand
{
}

public sealed class HeroCreateCommand : LegacyCoopCommand, IHeroCreateCommand
{
    public HeroCreateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "create_hero",
            "Creates a hero from a character template on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("characterObjectId", "The character template StringId."),
                new ExpectedArgs("age", "The optional hero age.", false),
            },
            HeroDebugCommand.CreateNewHero)
    {
    }
}

public interface IHeroAuditCommand : ICoopCommand
{
}

public sealed class HeroAuditCommand : LegacyCoopCommand, IHeroAuditCommand
{
    public HeroAuditCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "audit",
            "Audits registered hero state.",
            System.Array.Empty<IExpectedArgs>(),
            HeroDebugCommand.AuditHeroes)
    {
    }
}

public interface IHeroAddPowerCommand : ICoopCommand
{
}

public sealed class HeroAddPowerCommand : LegacyCoopCommand, IHeroAddPowerCommand
{
    public HeroAddPowerCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "add_power",
            "Adds power to a registered hero on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("power", "The integer power amount."),
            },
            HeroDebugCommand.AddPower)
    {
    }
}

public interface IHeroSetGoldCommand : ICoopCommand
{
}

public sealed class HeroSetGoldCommand : LegacyCoopCommand, IHeroSetGoldCommand
{
    public HeroSetGoldCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_gold",
            "Sets gold for every hero with an exact display name on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroName", "The exact hero display name. Quote multi-word values."),
                new ExpectedArgs("gold", "The integer gold value."),
            },
            HeroDebugCommand.SetGold)
    {
    }
}

public interface IHeroGoldStateCommand : ICoopCommand
{
}

public sealed class HeroGoldStateCommand : LegacyCoopCommand, IHeroGoldStateCommand
{
    public HeroGoldStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "gold_state",
            "Reports gold for a registered hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
            },
            HeroDebugCommand.GoldState)
    {
    }
}

public interface IHeroSetGoldStateCommand : ICoopCommand
{
}

public sealed class HeroSetGoldStateCommand : LegacyCoopCommand, IHeroSetGoldStateCommand
{
    public HeroSetGoldStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_gold_state",
            "Sets non-negative gold for a registered hero on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("gold", "The non-negative integer gold value."),
            },
            HeroDebugCommand.SetGoldState)
    {
    }
}

public interface IHeroSetHitpointsCommand : ICoopCommand
{
}

public sealed class HeroSetHitpointsCommand : LegacyCoopCommand, IHeroSetHitpointsCommand
{
    public HeroSetHitpointsCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_hitpoints",
            "Sets hit points for a registered hero on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("hitPoints", "The integer hit-point value."),
            },
            HeroDebugCommand.SetHeroHitPoints)
    {
    }
}

public interface IHeroSetBannerItemCommand : ICoopCommand
{
}

public sealed class HeroSetBannerItemCommand : LegacyCoopCommand, IHeroSetBannerItemCommand
{
    public HeroSetBannerItemCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_banneritem",
            "Sets the banner item for a registered hero on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("bannerItemId", "The banner item StringId."),
            },
            HeroDebugCommand.SetHeroBannerItem)
    {
    }
}

public interface IHeroListBannerItemsCommand : ICoopCommand
{
}

public sealed class HeroListBannerItemsCommand : LegacyCoopCommand, IHeroListBannerItemsCommand
{
    public HeroListBannerItemsCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "list_banneritems",
            "Lists available banner items.",
            System.Array.Empty<IExpectedArgs>(),
            HeroDebugCommand.ListBannerItems)
    {
    }
}

public interface IHeroGetBannerItemCommand : ICoopCommand
{
}

public sealed class HeroGetBannerItemCommand : LegacyCoopCommand, IHeroGetBannerItemCommand
{
    public HeroGetBannerItemCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "get_banneritem",
            "Reports the banner item for a registered hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
            },
            HeroDebugCommand.GetHeroBannerItem)
    {
    }
}

public interface IHeroIssuesCommand : ICoopCommand
{
}

public sealed class HeroIssuesCommand : LegacyCoopCommand, IHeroIssuesCommand
{
    public HeroIssuesCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "issues",
            "Lists heroes with active issues.",
            System.Array.Empty<IExpectedArgs>(),
            HeroDebugCommand.ListIssues)
    {
    }
}

public interface IHeroSetIssueCommand : ICoopCommand
{
}

public sealed class HeroSetIssueCommand : LegacyCoopCommand, IHeroSetIssueCommand
{
    public HeroSetIssueCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_issue",
            "Sets an issue for a registered hero on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
                new ExpectedArgs("issueId", "The issue StringId."),
            },
            HeroDebugCommand.SetHeroIssue)
    {
    }
}

public interface IHeroGetIssueCommand : ICoopCommand
{
}

public sealed class HeroGetIssueCommand : LegacyCoopCommand, IHeroGetIssueCommand
{
    public HeroGetIssueCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "get_issue",
            "Reports the issue for a registered hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The registered hero id."),
            },
            HeroDebugCommand.GetHeroIssue)
    {
    }
}

public interface IHeroVolunteersCommand : ICoopCommand
{
}

public sealed class HeroVolunteersCommand : LegacyCoopCommand, IHeroVolunteersCommand
{
    public HeroVolunteersCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "volunteers",
            "Lists volunteers for a hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("heroId", "The hero StringId."),
            },
            HeroDebugCommand.ViewVolunteersCommand)
    {
    }
}

public interface IHeroRefreshVolunteersCommand : ICoopCommand
{
}

public sealed class HeroRefreshVolunteersCommand : LegacyCoopCommand, IHeroRefreshVolunteersCommand
{
    public HeroRefreshVolunteersCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "refresh_volunteers",
            "Refreshes volunteers for a settlement on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("settlementId", "The optional settlement StringId; defaults to town_ES1.", false),
            },
            HeroDebugCommand.RefreshVolunteersCommand)
    {
    }
}

public interface IHeroSetRelationCommand : ICoopCommand
{
}

public sealed class HeroSetRelationCommand : LegacyCoopCommand, IHeroSetRelationCommand
{
    public HeroSetRelationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_relation",
            "Sets the base relation between two registered heroes.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("hero1Id", "The first registered hero id."),
                new ExpectedArgs("hero2Id", "The second registered hero id."),
                new ExpectedArgs("value", "The integer relation value."),
            },
            HeroDebugCommand.SetRelation)
    {
    }
}

public interface IHeroGetRelationCommand : ICoopCommand
{
}

public sealed class HeroGetRelationCommand : LegacyCoopCommand, IHeroGetRelationCommand
{
    public HeroGetRelationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "get_relation",
            "Reports the base relation between two registered heroes.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("hero1Id", "The first registered hero id."),
                new ExpectedArgs("hero2Id", "The second registered hero id."),
            },
            HeroDebugCommand.GetRelation)
    {
    }
}

public interface IHeroGetEffectiveRelationCommand : ICoopCommand
{
}

public sealed class HeroGetEffectiveRelationCommand : LegacyCoopCommand, IHeroGetEffectiveRelationCommand
{
    public HeroGetEffectiveRelationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "get_effective_relation",
            "Reports the effective relation between two registered heroes.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("hero1Id", "The first registered hero id."),
                new ExpectedArgs("hero2Id", "The second registered hero id."),
            },
            HeroDebugCommand.GetEffectiveRelation)
    {
    }
}

public interface IHeroSetEffectiveRelationCommand : ICoopCommand
{
}

public sealed class HeroSetEffectiveRelationCommand : LegacyCoopCommand, IHeroSetEffectiveRelationCommand
{
    public HeroSetEffectiveRelationCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.hero",
            "set_effective_relation",
            "Sets effective relation between two registered heroes.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("hero1Id", "The first registered hero id."),
                new ExpectedArgs("hero2Id", "The second registered hero id."),
                new ExpectedArgs("value", "The integer effective relation value."),
            },
            HeroDebugCommand.SetEffectiveRelation)
    {
    }
}

public interface IRomanceListCommand : ICoopCommand
{
}

public sealed class RomanceListCommand : LegacyCoopCommand, IRomanceListCommand
{
    public RomanceListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "list",
            "Lists current romance states.",
            System.Array.Empty<IExpectedArgs>(),
            RomanceDebugCommand.List)
    {
    }
}

public interface IRomanceHelpCommand : ICoopCommand
{
}

public sealed class RomanceHelpCommand : LegacyCoopCommand, IRomanceHelpCommand
{
    public RomanceHelpCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "help",
            "Describes the romance debug commands.",
            System.Array.Empty<IExpectedArgs>(),
            RomanceDebugCommand.Help)
    {
    }
}

public interface IRomanceStatusCommand : ICoopCommand
{
}

public sealed class RomanceStatusCommand : LegacyCoopCommand, IRomanceStatusCommand
{
    public RomanceStatusCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "status",
            "Reports romance state for a player and NPC.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Status)
    {
    }
}

public interface IRomanceStartCommand : ICoopCommand
{
}

public sealed class RomanceStartCommand : LegacyCoopCommand, IRomanceStartCommand
{
    public RomanceStartCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "start",
            "Starts courtship between a player and NPC.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Start)
    {
    }
}

public interface IRomanceCompatibleCommand : ICoopCommand
{
}

public sealed class RomanceCompatibleCommand : LegacyCoopCommand, IRomanceCompatibleCommand
{
    public RomanceCompatibleCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "compatible",
            "Marks a romance pair as compatible.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Compatible)
    {
    }
}

public interface IRomanceAgreeCommand : ICoopCommand
{
}

public sealed class RomanceAgreeCommand : LegacyCoopCommand, IRomanceAgreeCommand
{
    public RomanceAgreeCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "agree",
            "Marks a romance pair as agreed on marriage.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Agree)
    {
    }
}

public interface IRomanceMarryCommand : ICoopCommand
{
}

public sealed class RomanceMarryCommand : LegacyCoopCommand, IRomanceMarryCommand
{
    public RomanceMarryCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "marry",
            "Marries a player hero and NPC hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Marry)
    {
    }
}

public interface IRomanceDivorceCommand : ICoopCommand
{
}

public sealed class RomanceDivorceCommand : LegacyCoopCommand, IRomanceDivorceCommand
{
    public RomanceDivorceCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.romance",
            "divorce",
            "Divorces a player hero and NPC hero.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("playerHeroId", "The registered player hero id."),
                new ExpectedArgs("npcHeroId", "The registered NPC hero id."),
            },
            RomanceDebugCommand.Divorce)
    {
    }
}
