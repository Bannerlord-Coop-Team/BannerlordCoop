using Common.Commands;
using GameInterface.Services.Issues.Generic;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Issues.Commands;

public static class IssuesDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class IssuesGiveCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";

        public string Name => "give";

        public string Description => "Runs the give debug operation.";

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

    public sealed class IssuesCompleteCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.issues";

        public string Name => "complete";

        public string Description => "Runs the complete debug operation.";

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
