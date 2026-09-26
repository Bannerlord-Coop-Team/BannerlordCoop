using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Issues.Commands;

public static class IssuesDebugCommand
{
    private const string StolenGoodsKey = "GangLeaderNeedsToOffloadStolenGoods";
    private static readonly HashSet<Hero> StagedAcceptOwners = new();

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class IssuesGiveCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";

        public string Name => "give";

        public string Description => "Runs the give debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id.", isRequired: true),
            new ExpectedArgs("quest_type_key", "The issue type key.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.give")) return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);

            var key = args[1];
            if (!IssueGiveCatalog.TryGet(key, out var entry))
            {
                return Failed($"Unknown quest type key '{key}'. Use coop.debug.issues.list_types to see all valid keys.");
            }

            if (entry.Resolve == null)
            {
                return Failed($"Quest type '{key}' is a known vanilla Issue type but is not wired for give: {entry.NotWiredReason}");
            }

            if (hero.Issue != null)
            {
                return Failed($"Hero '{hero.Name}' (StringId '{hero.StringId}') already has an active issue " +
                    $"({hero.Issue.GetType().Name}, StringId '{hero.Issue.StringId}'). Complete or clear it first.");
            }

            (PotentialIssueData.StartIssueDelegate factory, string resolveError) = entry.Resolve(hero);
            if (factory == null)
            {
                return Failed($"Could not give '{key}' to hero '{hero.Name}': {resolveError}");
            }

            try
            {
                var pid = new PotentialIssueData(factory, entry.IssueType, IssueBase.IssueFrequency.Common);
                Campaign.Current.IssueManager.CreateNewIssue(in pid, hero);
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException($"coop.debug.issues.give ({key}): CreateNewIssue", ex));
            }

            return StartQuestOrRollback(hero, key);
        }
    }

    private static CoopCommandResult StartQuestOrRollback(Hero hero, string key)
    {
        bool started;
        try
        {
            using (new QuestSolutionStartAuthorityGuard())
            {
                started = Campaign.Current.IssueManager.StartIssueQuest(hero);
            }
        }
        catch (Exception ex)
        {
            var stuckIssue = hero.Issue;
            if (stuckIssue != null)
            {
                try
                {
                    Campaign.Current.IssueManager.DeactivateIssue(stuckIssue);
                }
                catch (Exception cleanupEx)
                {
                    return Failed(CommandHelpers.FormatException(
                        $"coop.debug.issues.give ({key}): StartIssueQuest threw ({ex.GetType().Name}: {ex.Message}), " +
                        $"then the DeactivateIssue rollback ALSO threw - hero '{hero.Name}' (StringId '{hero.StringId}') " +
                        "may still have a stuck issue attached", cleanupEx));
                }
            }

            return Failed(CommandHelpers.FormatException(
                $"coop.debug.issues.give ({key}): StartIssueQuest threw during quest construction. The issue has " +
                $"been rolled back via DeactivateIssue - hero '{hero.Name}' (StringId '{hero.StringId}') has no " +
                "issue attached and is safe to retry give with any quest type", ex));
        }

        if (!started)
        {
            return Failed($"Issue '{key}' was created for hero '{hero.Name}' but StartIssueQuest returned false " +
                "(its IssueStayAliveConditions failed immediately) - the game has already cleaned the issue up.");
        }

        return Succeeded($"Gave quest type '{key}' to hero '{hero.Name}' (StringId '{hero.StringId}'). " +
            $"Issue StringId: '{hero.Issue?.StringId}'. Quest StringId: '{hero.Issue?.IssueQuest?.StringId ?? "none"}'.");
    }

    public sealed class IssuesStageAcceptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "stage_accept";
        public string Description => "Stages an unaccepted gang leader stolen-goods issue for an eligible registered owner.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "An optional registered gang leader id; omit to select an eligible owner.", isRequired: false),
        };

        private static bool IsEligibleOwner(Hero hero)
        {
            var settlement = hero?.CurrentSettlement;
            return hero?.IsActive == true && hero.IsGangLeader && hero.Issue == null &&
                settlement?.IsTown == true && settlement.Town.Security < 70f &&
                settlement.Notables.Any(other => other != hero && other.IsActive && other.IsMerchant &&
                    other.CurrentSettlement == settlement);
        }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.stage_accept")) return Failed(error);
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return Failed(error);
            Hero hero;
            string ownerId;
            if (args.Count > 0)
            {
                ownerId = args[0];
                if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, ownerId, out hero, out error)) return Failed(error);
            }
            else
            {
                hero = Hero.AllAliveHeroes
                    .Where(candidate => IsEligibleOwner(candidate) && objectManager.TryGetId(candidate, out _))
                    .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (hero == null) return Failed("No registered eligible gang leader with an active merchant was found in a town.");
                objectManager.TryGetId(hero, out ownerId);
            }
            if (!IsEligibleOwner(hero)) return Failed($"Hero '{hero.Name}' is not an eligible town gang leader without an issue.");
            if (!IssueGiveCatalog.TryGet(StolenGoodsKey, out var entry)) return Failed("Stolen-goods issue is unavailable.");
            var (factory, resolveError) = entry.Resolve(hero);
            if (factory == null) return Failed(resolveError);

            try
            {
                var potential = new PotentialIssueData(factory, entry.IssueType, IssueBase.IssueFrequency.Common);
                Campaign.Current.IssueManager.CreateNewIssue(in potential, hero);
                if (hero.Issue == null || !hero.Issue.IsOngoingWithoutQuest || !hero.Issue.IssueStayAliveConditions())
                {
                    if (hero.Issue != null) Campaign.Current.IssueManager.DeactivateIssue(hero.Issue);
                    return Failed("Issue did not remain available for acceptance.");
                }
                StagedAcceptOwners.Add(hero);
                return Succeeded($"Staged unaccepted {StolenGoodsKey} for '{hero.Name}' ({ownerId}), issue '{hero.Issue.StringId}', " +
                    $"settlement '{hero.CurrentSettlement.StringId}'.");
            }
            catch (Exception ex)
            {
                if (hero.Issue != null) Campaign.Current.IssueManager.DeactivateIssue(hero.Issue);
                return Failed(CommandHelpers.FormatException("coop.debug.issues.stage_accept", ex));
            }
        }
    }

    public sealed class IssuesOpenAcceptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "open_accept";
        public string Description => "Opens the normal tracked issue conversation from a client.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue owner id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on a client.");
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            if (QuestTypeRegistry.Get(hero.Issue)?.SupportsQuestSolutionAccept != true || !hero.Issue.IsOngoingWithoutQuest)
                return Failed("The owner has no unaccepted synchronized issue.");
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
                return Failed("Client controller id is unavailable.");
            MessageBroker.Instance.Publish(hero, new IssueConversationOpenedLocally(hero, controllerIdProvider.ControllerId));
            return Succeeded($"Requested tracked conversation with '{hero.Name}' ({args[0]}). Wait for the server allowance before accepting.");
        }
    }

    public sealed class IssuesAcceptPersonalCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "accept_personal";
        public string Description => "Uses the normal client quest-acceptance method.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue owner id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on a client.");
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            if (QuestTypeRegistry.Get(hero.Issue)?.SupportsQuestSolutionAccept != true || !hero.Issue.IsOngoingWithoutQuest)
                return Failed("The owner has no unaccepted synchronized issue.");
            return hero.Issue.StartIssueWithQuest()
                ? Succeeded($"Requested personal acceptance for '{hero.Name}' ({args[0]}).")
                : Failed("The local acceptance method rejected the issue.");
        }
    }

    public sealed class IssuesAcceptAlternativeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "accept_alternative";
        public string Description => "Selects a companion and six troops, then uses the normal client alternative-acceptance method.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue owner id.", isRequired: true),
            new ExpectedArgs("companion_id", "The registered companion hero id.", isRequired: true),
            new ExpectedArgs("troop_string_id", "The ordinary troop StringId in the client party.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Run this command on a client.");
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[1], out var companion, out error)) return Failed(error);
            if (QuestTypeRegistry.Get(hero.Issue)?.SupportsAlternativeAccept != true || !hero.Issue.IsOngoingWithoutQuest)
                return Failed("The owner has no unaccepted alternative issue.");
            var party = MobileParty.MainParty;
            if (party == null || companion == Hero.MainHero || companion.CharacterObject == null ||
                party.MemberRoster.GetTroopCount(companion.CharacterObject) != 1)
                return Failed("The companion must be present in this client's party.");
            var troop = party.MemberRoster.GetTroopRoster().FirstOrDefault(element =>
                element.Character?.StringId == args[2] && !element.Character.IsHero);
            if (troop.Character == null || troop.Number - troop.WoundedNumber < 6)
                return Failed("The client party needs six healthy troops of that StringId.");
            var roster = hero.Issue.AlternativeSolutionSentTroops;
            if (roster.TotalManCount != 0) return Failed("The issue already has a troop selection.");
            var before = TroopRoster.CreateDummyTroopRoster();
            before.Add(roster);
            var companionElement = party.MemberRoster.GetElementCopyAtIndex(
                party.MemberRoster.FindIndexOfTroop(companion.CharacterObject));
            var troopXp = troop.Xp * 6 / troop.Number;
            using (new Common.Util.AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, -1, false, 0, -companionElement.Xp, true);
                roster.AddToCounts(companion.CharacterObject, 1, false, 0, companionElement.Xp, true);
                party.MemberRoster.AddToCounts(troop.Character, -6, false, 0, -troopXp, true);
                roster.AddToCounts(troop.Character, 6, false, 0, troopXp, true);
            }
            var after = TroopRoster.CreateDummyTroopRoster();
            after.Add(roster);
            MessageBroker.Instance.Publish(roster, new QuestAlternativeTroopsTransferredLocally(roster, before, after));
            hero.Issue.StartIssueWithAlternativeSolution();
            return Succeeded($"Requested alternative acceptance for '{hero.Name}' ({args[0]}) with companion '{companion.Name}' and six {troop.Character.Name}.");
        }
    }

    public sealed class IssuesObserveAcceptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "observe_accept";
        public string Description => "Reports the issue acceptance state and selected troop roster.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue owner id.", isRequired: true),
            new ExpectedArgs("controller_id", "An optional requester controller id for conversation tracking.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            var issue = hero.Issue;
            if (issue == null) return Succeeded($"owner={args[0]} issue=none");
            ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generations);
            ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var owners);
            var generation = generations != null && generations.TryGetGeneration(hero, out var value) ? value.ToString() : "none";
            var controller = owners != null && owners.TryGetOwnerControllerId(hero, out var id) ? id : "none";
            var trackedConversation = "";
            if (args.Count > 1)
            {
                ContainerProvider.TryResolve<IIssueConversationTracker>(out var conversations);
                var trackedGeneration = conversations != null &&
                    conversations.TryGetTrackedRequester(args[0], args[1], out var currentGeneration)
                    ? currentGeneration.ToString() : "none";
                trackedConversation = $" trackedConversationGeneration={trackedGeneration}";
            }
            var troops = string.Join(",", issue.AlternativeSolutionSentTroops.GetTroopRoster()
                .Select(element => $"{element.Character.StringId}:{element.Number}:{element.WoundedNumber}:{element.Xp}"));
            return Succeeded($"owner={args[0]} issue={issue.StringId} ongoingWithoutQuest={issue.IsOngoingWithoutQuest} " +
                $"quest={issue.IssueQuest?.StringId ?? "none"} alternative={issue.IsSolvingWithAlternative} " +
                $"generation={generation} controller={controller}{trackedConversation} sentTroops=[{troops}]");
        }
    }

    public sealed class IssuesResetAcceptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";
        public string Name => "reset_accept";
        public string Description => "Cancels an issue staged by this debug fixture.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered staged issue owner id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.reset_accept")) return Failed(error);
            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            if (!StagedAcceptOwners.Contains(hero)) return Failed("This owner was not staged by the acceptance fixture.");
            try
            {
                if (hero.Issue != null)
                {
                    using (new IssueFinalizeAuthorityGuard()) hero.Issue.CompleteIssueWithCancel();
                }
                StagedAcceptOwners.Remove(hero);
                return Succeeded($"Reset staged issue for '{hero.Name}' ({args[0]}).");
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("coop.debug.issues.reset_accept", ex));
            }
        }
    }

    public sealed class IssuesCompleteCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";

        public string Name => "complete";

        public string Description => "Runs the complete debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue owner hero id.", isRequired: true),
            new ExpectedArgs("outcome", "success, cancel, fail, timeout, or betrayal.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.complete")) return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);

            if (hero.Issue == null)
            {
                return Failed($"Hero '{hero.Name}' (StringId '{hero.StringId}') has no active issue.");
            }

            var quest = hero.Issue.IssueQuest;
            if (quest == null)
            {
                return Failed($"Hero '{hero.Name}' has an active issue ({hero.Issue.GetType().Name}) but no live quest yet. " +
                    "Use coop.debug.issues.give (or the natural accept flow) to start the quest before completing it.");
            }

            var outcome = args.Count == 2 ? args[1].Trim().ToLowerInvariant() : "success";

            try
            {
                using (new IssueFinalizeAuthorityGuard())
                {
                    switch (outcome)
                    {
                        case "success":
                            quest.CompleteQuestWithSuccess();
                            break;
                        case "cancel":
                            quest.CompleteQuestWithCancel();
                            break;
                        case "fail":
                            quest.CompleteQuestWithFail();
                            break;
                        case "timeout":
                            quest.CompleteQuestWithTimeOut();
                            break;
                        case "betrayal":
                            quest.CompleteQuestWithBetrayal();
                            break;
                        default:
                            return Failed($"Unknown outcome '{outcome}'. Expected success, cancel, fail, timeout, or betrayal.");
                    }
                }
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException($"coop.debug.issues.complete ({outcome})", ex));
            }

            return Succeeded($"Completed quest for hero '{hero.Name}' (StringId '{hero.StringId}') with outcome '{outcome}'.");
        }
    }

    public sealed class IssuesListTypesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";

        public string Name => "list_types";

        public string Description => "Reports list types.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();

            foreach (var entry in IssueGiveCatalog.Entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(entry.Resolve != null
                    ? $"{entry.Key} [wired]"
                    : $"{entry.Key} [not wired: {entry.NotWiredReason}]");
            }

            return Succeeded(sb.ToString());
        }
    }
}
