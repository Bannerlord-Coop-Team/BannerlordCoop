using Common;
using GameInterface;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using static TaleWorlds.Library.CommandLineFunctionality;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Commands;

internal class RomanceDebugCommand
{
    private const string CommandNamespace = "coop.debug.romance";
    private const string PairUsage = "<playerHeroId> <npcHeroId>";

    [CommandLineArgumentFunction("list", CommandNamespace)]
    public static string List(List<string> args)
    {
        const string command = CommandNamespace + ".list";
        var context = new CommandContext(command, $"Usage: {command}", args);
        if (!context.RequireArgCount(0, out var error)) return error;

        return RunOnGameThread(command, () => ListStates());
    }

    [CommandLineArgumentFunction("status", CommandNamespace)]
    public static string Status(List<string> args)
    {
        const string command = CommandNamespace + ".status";
        var context = new CommandContext(command, $"Usage: {command} {PairUsage}", args);
        if (!context.RequireArgCount(2, out var error)) return error;

        return RunOnGameThread(command, () =>
        {
            if (!TryGetPlayerNpcPair(context.Args, out var playerHero, out var targetHero, out error)) return error;

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return error;

            var state = Romance.GetRomanticState(playerHero, targetHero);
            return state == null
                ? $"No romance state exists between {FormatHero(playerHero, objectManager)} and {FormatHero(targetHero, objectManager)}."
                : $"{FormatState(state, objectManager)}; " +
                  $"spouses={playerHero.Spouse?.StringId ?? "none"}/{targetHero.Spouse?.StringId ?? "none"}";
        });
    }

    [CommandLineArgumentFunction("help", CommandNamespace)]
    public static string Help(List<string> args)
    {
        if (args.Count != 0) return $"Usage: {CommandNamespace}.help";

        return $"{CommandNamespace}.list; {CommandNamespace}.status {PairUsage}; " +
               $"{CommandNamespace}.start|compatible|agree|marry|divorce {PairUsage}. " +
               "Only start, compatible, agree, marry, and divorce require the server console. " +
               "Divorce does not restore pre-marriage clan or party changes.";
    }

    [CommandLineArgumentFunction("start", CommandNamespace)]
    public static string Start(List<string> args)
        => ChangeState(args, "start", Romance.RomanceLevelEnum.CourtshipStarted);

    [CommandLineArgumentFunction("compatible", CommandNamespace)]
    public static string Compatible(List<string> args)
        => ChangeState(args, "compatible", Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible);

    [CommandLineArgumentFunction("agree", CommandNamespace)]
    public static string Agree(List<string> args)
        => ChangeState(args, "agree", Romance.RomanceLevelEnum.CoupleAgreedOnMarriage);

    [CommandLineArgumentFunction("marry", CommandNamespace)]
    public static string Marry(List<string> args)
    {
        const string command = CommandNamespace + ".marry";
        var context = new CommandContext(command, $"Usage: {command} {PairUsage}", args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(2, out error)) return error;

        return RunOnGameThread(command, () =>
        {
            if (!TryGetPlayerNpcPair(context.Args, out var playerHero, out var targetHero, out error)) return error;
            if (!TryGetRomanceAuthority(out var romanceAuthority, out error)) return error;
            if (!romanceAuthority.TryValidateMarriage(playerHero, targetHero, out error)) return error;

            MarriageAction.Apply(playerHero, targetHero);
            return playerHero.Spouse == targetHero && targetHero.Spouse == playerHero
                ? $"Married {playerHero.Name} to {targetHero.Name}."
                : $"Marriage between {playerHero.Name} and {targetHero.Name} did not complete.";
        });
    }

    [CommandLineArgumentFunction("divorce", CommandNamespace)]
    public static string Divorce(List<string> args)
    {
        const string command = CommandNamespace + ".divorce";
        var context = new CommandContext(command, $"Usage: {command} {PairUsage}", args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(2, out error)) return error;

        return RunOnGameThread(command, () =>
        {
            if (!TryGetPlayerNpcPair(context.Args, out var playerHero, out var targetHero, out error)) return error;
            if (playerHero.Spouse != targetHero || targetHero.Spouse != playerHero)
                return $"{playerHero.Name} and {targetHero.Name} are not married to each other.";

            playerHero.Spouse = null;
            ChangeRomanticStateAction.Apply(playerHero, targetHero, Romance.RomanceLevelEnum.Ended);

            return playerHero.Spouse == null &&
                   targetHero.Spouse == null &&
                   Romance.GetRomanticLevel(playerHero, targetHero) == Romance.RomanceLevelEnum.Ended
                ? $"Divorced {playerHero.Name} and {targetHero.Name}."
                : $"Divorce between {playerHero.Name} and {targetHero.Name} did not complete.";
        });
    }

    private static string ChangeState(
        List<string> args,
        string action,
        Romance.RomanceLevelEnum requestedLevel)
    {
        var command = $"{CommandNamespace}.{action}";
        var context = new CommandContext(command, $"Usage: {command} {PairUsage}", args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(2, out error)) return error;

        return RunOnGameThread(command, () =>
        {
            if (!TryGetPlayerNpcPair(context.Args, out var playerHero, out var targetHero, out error)) return error;
            if (!TryGetRomanceAuthority(out var romanceAuthority, out error)) return error;
            if (!romanceAuthority.TryValidateStateChange(playerHero, targetHero, requestedLevel, out error)) return error;

            ChangeRomanticStateAction.Apply(playerHero, targetHero, requestedLevel);
            return $"Changed romance between {playerHero.Name} and {targetHero.Name} to {requestedLevel}.";
        });
    }

    private static string ListStates()
    {
        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error)) return error;

        var states = Romance.RomanticStateList;
        if (states == null || states.Count == 0) return "No romance states exist.";

        var result = new StringBuilder();
        foreach (var state in states)
        {
            if (state != null) result.AppendLine(FormatState(state, objectManager));
        }

        return result.Length == 0 ? "No romance states exist." : result.ToString();
    }

    private static bool TryGetPlayerNpcPair(
        List<string> args,
        out Hero playerHero,
        out Hero targetHero,
        out string error)
    {
        playerHero = null;
        targetHero = null;
        error = null;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error)) return false;
        if (!objectManager.TryGetObject<Hero>(args[0], out playerHero))
        {
            error = $"Unable to find player hero with id: {args[0]}";
            return false;
        }

        if (!objectManager.TryGetObject<Hero>(args[1], out targetHero))
        {
            error = $"Unable to find NPC hero with id: {args[1]}";
            return false;
        }

        if (!playerHero.IsPlayerHero())
        {
            error = $"Hero '{args[0]}' is not player-controlled.";
            return false;
        }

        if (targetHero.IsPlayerHero())
        {
            error = "Player-to-player romance is not supported.";
            return false;
        }

        return true;
    }

    private static string RunOnGameThread(string command, Func<string> action)
    {
        var result = $"{command} did not complete.";
        GameThread.RunSafe(() => result = action(), blocking: true, context: command);
        return result;
    }

    private static bool TryGetRomanceAuthority(out IRomanceAuthority romanceAuthority, out string error)
    {
        if (ContainerProvider.TryResolve(out romanceAuthority))
        {
            error = null;
            return true;
        }

        error = "Could not resolve RomanceAuthority from container.";
        return false;
    }

    private static string FormatState(Romance.RomanticState state, IObjectManager objectManager)
        => $"{FormatHero(state.Person1, objectManager)} <-> {FormatHero(state.Person2, objectManager)}: " +
           $"{state.Level}, progress={state.ProgressToNextLevel}, lastVisit={state.LastVisit}, persuasion={state.ScoreFromPersuasion}";

    private static string FormatHero(Hero hero, IObjectManager objectManager)
    {
        if (hero == null) return "<missing hero>";

        return objectManager.TryGetId(hero, out var heroId)
            ? $"{heroId} ({hero.Name})"
            : $"<unregistered> ({hero.Name})";
    }
}
