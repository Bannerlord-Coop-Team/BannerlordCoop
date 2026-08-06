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
    // coop.debug.issues.give lord_2_7 VillageNeedsTools
    [CommandLineArgumentFunction("give", "coop.debug.issues")]
    public static string Give(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.give")) return error;

        const string usage = "Usage: coop.debug.issues.give <heroId> <questTypeKey> (see coop.debug.issues.list_types)";

        if (!CommandHelpers.HasArgCount(args, 2, usage, out error)) return error;
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return error;

        var key = args[1];
        if (!IssueGiveCatalog.TryGet(key, out var entry))
        {
            return $"Unknown quest type key '{key}'. Use coop.debug.issues.list_types to see all valid keys.";
        }

        if (entry.Resolve == null)
        {
            return $"Quest type '{key}' is a known vanilla Issue type but is not wired for give: {entry.NotWiredReason}";
        }

        // Vanilla only ever allows one active issue per hero at a time (IssueManager.Issues is keyed by
        // Hero) - reject rather than silently overwrite an existing one.
        if (hero.Issue != null)
        {
            return $"Hero '{hero.Name}' (StringId '{hero.StringId}') already has an active issue " +
                $"({hero.Issue.GetType().Name}, StringId '{hero.Issue.StringId}'). Complete or clear it first.";
        }

        (PotentialIssueData.StartIssueDelegate factory, string resolveError) = entry.Resolve(hero);
        if (factory == null)
        {
            return $"Could not give '{key}' to hero '{hero.Name}': {resolveError}";
        }

        try
        {
            var pid = new PotentialIssueData(factory, entry.IssueType, IssueBase.IssueFrequency.Common);
            Campaign.Current.IssueManager.CreateNewIssue(in pid, hero);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException($"coop.debug.issues.give ({key}): CreateNewIssue", ex);
        }

        return StartQuestOrRollback(hero, key);
    }

    // A throw here leaves hero.Issue stuck attached with no live quest - roll it back via DeactivateIssue.
    private static string StartQuestOrRollback(Hero hero, string key)
    {
        bool started;
        try
        {
            started = Campaign.Current.IssueManager.StartIssueQuest(hero);
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
                    return CommandHelpers.FormatException(
                        $"coop.debug.issues.give ({key}): StartIssueQuest threw ({ex.GetType().Name}: {ex.Message}), " +
                        $"then the DeactivateIssue rollback ALSO threw - hero '{hero.Name}' (StringId '{hero.StringId}') " +
                        "may still have a stuck issue attached", cleanupEx);
                }
            }

            return CommandHelpers.FormatException(
                $"coop.debug.issues.give ({key}): StartIssueQuest threw during quest construction. The issue has " +
                $"been rolled back via DeactivateIssue - hero '{hero.Name}' (StringId '{hero.StringId}') has no " +
                "issue attached and is safe to retry give with any quest type", ex);
        }

        if (!started)
        {
            return $"Issue '{key}' was created for hero '{hero.Name}' but StartIssueQuest returned false " +
                "(its IssueStayAliveConditions failed immediately) - the game has already cleaned the issue up.";
        }

        return $"Gave quest type '{key}' to hero '{hero.Name}' (StringId '{hero.StringId}'). " +
            $"Issue StringId: '{hero.Issue?.StringId}'. Quest StringId: '{hero.Issue?.IssueQuest?.StringId ?? "none"}'.";
    }

    // coop.debug.issues.complete lord_2_7
    // coop.debug.issues.complete lord_2_7 fail
    [CommandLineArgumentFunction("complete", "coop.debug.issues")]
    public static string Complete(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.complete")) return error;

        const string usage = "Usage: coop.debug.issues.complete <heroId> [success|cancel|fail|timeout|betrayal]";

        if (args == null || args.Count < 1 || args.Count > 2)
        {
            return usage;
        }

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return error;
        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return error;

        if (hero.Issue == null)
        {
            return $"Hero '{hero.Name}' (StringId '{hero.StringId}') has no active issue.";
        }

        var quest = hero.Issue.IssueQuest;
        if (quest == null)
        {
            return $"Hero '{hero.Name}' has an active issue ({hero.Issue.GetType().Name}) but no live quest yet. " +
                "Use coop.debug.issues.give (or the natural accept flow) to start the quest before completing it.";
        }

        var outcome = args.Count == 2 ? args[1].Trim().ToLowerInvariant() : "success";

        try
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
                    return $"Unknown outcome '{outcome}'. {usage}";
            }
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException($"coop.debug.issues.complete ({outcome})", ex);
        }

        return $"Completed quest for hero '{hero.Name}' (StringId '{hero.StringId}') with outcome '{outcome}'.";
    }

    // coop.debug.issues.list_types
    [CommandLineArgumentFunction("list_types", "coop.debug.issues")]
    public static string ListTypes(List<string> args)
    {
        var sb = new StringBuilder();

        foreach (var entry in IssueGiveCatalog.Entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(entry.Resolve != null
                ? $"{entry.Key} [wired]"
                : $"{entry.Key} [not wired: {entry.NotWiredReason}]");
        }

        return sb.ToString();
    }
}
