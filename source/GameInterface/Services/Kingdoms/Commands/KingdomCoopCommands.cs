using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Kingdoms.Commands;

public interface IKingdomOpenCommand : ICoopCommand
{
}

public sealed class KingdomOpenCommand : LegacyCoopCommand, IKingdomOpenCommand
{
    public KingdomOpenCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "open",
            "Opens the kingdom screen on a client.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.OpenKingdomScreen)
    {
    }
}

public interface IKingdomOpenDecisionCommand : ICoopCommand
{
}

public sealed class KingdomOpenDecisionCommand : LegacyCoopCommand, IKingdomOpenDecisionCommand
{
    public KingdomOpenDecisionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "open_decision",
            "Opens one queued kingdom decision.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("decisionIndex", "The one-based decision index."),
            },
            KingdomDebugCommand.OpenKingdomDecisionScreen)
    {
    }
}

public interface IKingdomCloseCommand : ICoopCommand
{
}

public sealed class KingdomCloseCommand : LegacyCoopCommand, IKingdomCloseCommand
{
    public KingdomCloseCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "close",
            "Closes the kingdom screen on a client.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.CloseKingdomScreen)
    {
    }
}

public interface IKingdomScreenStateCommand : ICoopCommand
{
}

public sealed class KingdomScreenStateCommand : LegacyCoopCommand, IKingdomScreenStateCommand
{
    public KingdomScreenStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "screen_state",
            "Reports kingdom screen state.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.KingdomScreenState)
    {
    }
}

public interface IKingdomPolicyTimeoutCaptureCommand : ICoopCommand
{
}

public sealed class KingdomPolicyTimeoutCaptureCommand : LegacyCoopCommand, IKingdomPolicyTimeoutCaptureCommand
{
    public KingdomPolicyTimeoutCaptureCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "policy_timeout_capture",
            "Captures the kingdom policy-timeout fixture.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.CapturePolicyTimeoutFixture)
    {
    }
}

public interface IKingdomPolicyTimeoutStageCommand : ICoopCommand
{
}

public sealed class KingdomPolicyTimeoutStageCommand : LegacyCoopCommand, IKingdomPolicyTimeoutStageCommand
{
    public KingdomPolicyTimeoutStageCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "policy_timeout_stage",
            "Stages the kingdom policy-timeout fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("proposerClanId", "The registered proposer clan id."),
                new ExpectedArgs("policyId", "The registered policy id."),
                new ExpectedArgs("policyWasActive", "Whether the policy was active at capture."),
            },
            KingdomDebugCommand.StagePolicyTimeoutFixture)
    {
    }
}

public interface IKingdomPolicyTimeoutStateCommand : ICoopCommand
{
}

public sealed class KingdomPolicyTimeoutStateCommand : LegacyCoopCommand, IKingdomPolicyTimeoutStateCommand
{
    public KingdomPolicyTimeoutStateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "policy_timeout_state",
            "Reports kingdom policy-timeout fixture state.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.GetPolicyTimeoutState)
    {
    }
}

public interface IKingdomPolicyTimeoutRestoreCommand : ICoopCommand
{
}

public sealed class KingdomPolicyTimeoutRestoreCommand : LegacyCoopCommand, IKingdomPolicyTimeoutRestoreCommand
{
    public KingdomPolicyTimeoutRestoreCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "policy_timeout_restore",
            "Restores the kingdom policy-timeout fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("proposerClanId", "The registered proposer clan id."),
                new ExpectedArgs("policyId", "The registered policy id."),
                new ExpectedArgs("policyWasActive", "Whether the policy was active at capture."),
            },
            KingdomDebugCommand.RestorePolicyTimeoutFixture)
    {
    }
}

public interface IKingdomPolicyTimeoutVerifyCommand : ICoopCommand
{
}

public sealed class KingdomPolicyTimeoutVerifyCommand : LegacyCoopCommand, IKingdomPolicyTimeoutVerifyCommand
{
    public KingdomPolicyTimeoutVerifyCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "policy_timeout_verify",
            "Verifies the kingdom policy-timeout fixture.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("policyId", "The registered policy id."),
                new ExpectedArgs("policyWasActive", "Whether the policy was active at capture."),
            },
            KingdomDebugCommand.VerifyPolicyTimeoutFixture)
    {
    }
}

public interface IKingdomCreateCommand : ICoopCommand
{
}

public sealed class KingdomCreateCommand : LegacyCoopCommand, IKingdomCreateCommand
{
    public KingdomCreateCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "create",
            "Creates a kingdom for a clan leader on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("leaderHeroName", "The exact leader display name or id. Quote multi-word names."),
                new ExpectedArgs("kingdomName", "The kingdom display name. Quote multi-word values."),
            },
            KingdomDebugCommand.CreateKingdomCommand)
    {
    }
}

public interface IKingdomListCommand : ICoopCommand
{
}

public sealed class KingdomListCommand : LegacyCoopCommand, IKingdomListCommand
{
    public KingdomListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "list",
            "Lists campaign kingdoms.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.ListKingdoms)
    {
    }
}

public interface IKingdomInfoCommand : ICoopCommand
{
}

public sealed class KingdomInfoCommand : LegacyCoopCommand, IKingdomInfoCommand
{
    public KingdomInfoCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "info",
            "Reports state for a registered kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.Info)
    {
    }
}

public interface IKingdomForcePlayerJoinCommand : ICoopCommand
{
}

public sealed class KingdomForcePlayerJoinCommand : LegacyCoopCommand, IKingdomForcePlayerJoinCommand
{
    public KingdomForcePlayerJoinCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "force_player_join_kingdom",
            "Moves a player clan into a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.ForcePlayerJoinKingdom)
    {
    }
}

public interface IKingdomForcePlayerVassalageCommand : ICoopCommand
{
}

public sealed class KingdomForcePlayerVassalageCommand : LegacyCoopCommand, IKingdomForcePlayerVassalageCommand
{
    public KingdomForcePlayerVassalageCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "force_player_vassalage",
            "Requests player vassalage in a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("controllerId", "The player controller id."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("grantRewards", "Whether vassalage rewards should be granted.", false),
            },
            KingdomDebugCommand.ForcePlayerVassalage)
    {
    }
}

public interface IKingdomAddDecisionUsageCommand : ICoopCommand
{
}

public sealed class KingdomAddDecisionUsageCommand : LegacyCoopCommand, IKingdomAddDecisionUsageCommand
{
    public KingdomAddDecisionUsageCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "add_decision_usage",
            "Lists supported kingdom decision arguments.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.AddDecisionUsage)
    {
    }
}

public interface IKingdomRemoveDecisionUsageCommand : ICoopCommand
{
}

public sealed class KingdomRemoveDecisionUsageCommand : LegacyCoopCommand, IKingdomRemoveDecisionUsageCommand
{
    public KingdomRemoveDecisionUsageCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "remove_decision_usage",
            "Describes kingdom decision removal arguments.",
            System.Array.Empty<IExpectedArgs>(),
            KingdomDebugCommand.RemoveDecisionUsage)
    {
    }
}

public interface IKingdomListDecisionsCommand : ICoopCommand
{
}

public sealed class KingdomListDecisionsCommand : LegacyCoopCommand, IKingdomListDecisionsCommand
{
    public KingdomListDecisionsCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "list_kingdom_decisions",
            "Lists queued decisions for a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.ListKingdomDecisions)
    {
    }
}

public interface IKingdomDecisionsCommand : ICoopCommand
{
}

public sealed class KingdomDecisionsCommand : LegacyCoopCommand, IKingdomDecisionsCommand
{
    public KingdomDecisionsCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "decisions",
            "Lists queued decisions and client votes for a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.ListKingdomDecisionVotes)
    {
    }
}

public interface IKingdomListDecisionOutcomesCommand : ICoopCommand
{
}

public sealed class KingdomListDecisionOutcomesCommand : LegacyCoopCommand, IKingdomListDecisionOutcomesCommand
{
    public KingdomListDecisionOutcomesCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "list_decision_outcomes",
            "Lists possible outcomes for a kingdom decision.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("decisionIndex", "The one-based decision index."),
            },
            KingdomDebugCommand.ListKingdomDecisionOutcomes)
    {
    }
}

public interface IKingdomVoteDecisionCommand : ICoopCommand
{
}

public sealed class KingdomVoteDecisionCommand : LegacyCoopCommand, IKingdomVoteDecisionCommand
{
    public KingdomVoteDecisionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "vote_decision",
            "Requests a client vote on a kingdom decision.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("decisionIndex", "The one-based decision index."),
                new ExpectedArgs("outcome", "A one-based outcome index or abstain."),
                new ExpectedArgs("supportWeight", "The support weight name or value."),
                new ExpectedArgs("isFinal", "Whether this is the final vote.", false),
            },
            KingdomDebugCommand.VoteKingdomDecision)
    {
    }
}

public interface IKingdomResolveDecisionCommand : ICoopCommand
{
}

public sealed class KingdomResolveDecisionCommand : LegacyCoopCommand, IKingdomResolveDecisionCommand
{
    public KingdomResolveDecisionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "resolve_decision",
            "Resolves a queued kingdom decision.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("decisionIndex", "The one-based decision index."),
            },
            KingdomDebugCommand.ResolveKingdomDecision)
    {
    }
}

public interface IKingdomListPoliciesCommand : ICoopCommand
{
}

public sealed class KingdomListPoliciesCommand : LegacyCoopCommand, IKingdomListPoliciesCommand
{
    public KingdomListPoliciesCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "list_policies",
            "Lists active policies for a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.ListKingdomPolicies)
    {
    }
}

public interface IKingdomCollectionListCommand : ICoopCommand
{
}

public sealed class KingdomCollectionListCommand : LegacyCoopCommand, IKingdomCollectionListCommand
{
    public KingdomCollectionListCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "collection_list",
            "Lists a synced kingdom collection.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("collection", "The kingdom collection name."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
            },
            KingdomDebugCommand.ListKingdomCollection)
    {
    }
}

public interface IKingdomCollectionAddCommand : ICoopCommand
{
}

public sealed class KingdomCollectionAddCommand : LegacyCoopCommand, IKingdomCollectionAddCommand
{
    public KingdomCollectionAddCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "collection_add",
            "Adds a value to a synced kingdom collection.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("collection", "The kingdom collection name."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("valueId", "The registered value id or proposer clan id."),
                new ExpectedArgs("ignoreInfluenceCost", "For unresolvedDecisions, whether influence is ignored.", false),
                new ExpectedArgs("decisionType", "For unresolvedDecisions, the decision type.", false),
                new ExpectedArgs("decisionArg1", "The first fixed decision-specific argument.", false),
                new ExpectedArgs("decisionArg2", "The second fixed decision-specific argument.", false),
                new ExpectedArgs("decisionArg3", "The third fixed decision-specific argument.", false),
            },
            KingdomDebugCommand.AddKingdomCollectionItem)
    {
    }
}

public interface IKingdomCollectionRemoveCommand : ICoopCommand
{
}

public sealed class KingdomCollectionRemoveCommand : LegacyCoopCommand, IKingdomCollectionRemoveCommand
{
    public KingdomCollectionRemoveCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "collection_remove",
            "Removes a value from a synced kingdom collection.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("collection", "The kingdom collection name."),
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("valueIdOrIndex", "The registered value id or one-based decision index."),
            },
            KingdomDebugCommand.RemoveKingdomCollectionItem)
    {
    }
}

public interface IKingdomDeclareWarCommand : ICoopCommand
{
}

public sealed class KingdomDeclareWarCommand : LegacyCoopCommand, IKingdomDeclareWarCommand
{
    public KingdomDeclareWarCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "declare_war",
            "Declares war between two factions on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("faction1Id", "The first registered faction id."),
                new ExpectedArgs("faction2Id", "The second registered faction id."),
            },
            KingdomDebugCommand.DeclareWar)
    {
    }
}

public interface IKingdomMakePeaceCommand : ICoopCommand
{
}

public sealed class KingdomMakePeaceCommand : LegacyCoopCommand, IKingdomMakePeaceCommand
{
    public KingdomMakePeaceCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "make_peace",
            "Makes peace between two factions on the server.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("faction1Id", "The first registered faction id."),
                new ExpectedArgs("faction2Id", "The second registered faction id."),
            },
            KingdomDebugCommand.MakePeace)
    {
    }
}

public interface IKingdomAddDecisionCommand : ICoopCommand
{
}

public sealed class KingdomAddDecisionCommand : LegacyCoopCommand, IKingdomAddDecisionCommand
{
    public KingdomAddDecisionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "add_decision",
            "Adds a supported decision to a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("proposerClanId", "The registered proposer clan id."),
                new ExpectedArgs("ignoreInfluenceCost", "Whether influence cost is ignored."),
                new ExpectedArgs("decisionType", "The decision type."),
                new ExpectedArgs("decisionArg1", "The first fixed decision-specific argument.", false),
                new ExpectedArgs("decisionArg2", "The second fixed decision-specific argument.", false),
                new ExpectedArgs("decisionArg3", "The third fixed decision-specific argument.", false),
            },
            KingdomDebugCommand.AddDecision)
    {
    }
}

public interface IKingdomRemoveDecisionCommand : ICoopCommand
{
}

public sealed class KingdomRemoveDecisionCommand : LegacyCoopCommand, IKingdomRemoveDecisionCommand
{
    public KingdomRemoveDecisionCommand(ILegacyCoopCommandExecutor executor)
        : base(
            executor,
            "coop.debug.kingdom",
            "remove_decision",
            "Removes a queued decision from a kingdom.",
            new IExpectedArgs[]
            {
                new ExpectedArgs("kingdomId", "The registered kingdom id."),
                new ExpectedArgs("decisionIndex", "The one-based decision index."),
            },
            KingdomDebugCommand.RemoveDecision)
    {
    }
}
