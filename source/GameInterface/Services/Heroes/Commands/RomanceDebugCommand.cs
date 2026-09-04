using Common;
using Common.Commands;
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
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Commands;

internal class RomanceDebugCommand
{
    private const string CommandNamespace = "coop.debug.romance";

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class RomanceListCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "list";

        public string Description => "Lists current romance states.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".list";
            return RunOnGameThread(command, () =>
            {
                if (!CommandHelpers.TryGetObjectManager(out var objectManager, out string error))
                    return Failed(error);

                var states = Romance.RomanticStateList;
                if (states == null || states.Count == 0)
                    return Succeeded("No romance states exist.");

                var result = new StringBuilder();
                foreach (var state in states)
                {
                    if (state != null) result.AppendLine(FormatState(state, objectManager));
                }

                return Succeeded(result.Length == 0 ? "No romance states exist." : result.ToString());
            });
        }
    }

    public sealed class RomanceHelpCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "help";

        public string Description => "Describes the romance debug commands.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return Succeeded($"{CommandNamespace}.list; {CommandNamespace}.status <player_hero_id> <npc_hero_id>; " +
                $"{CommandNamespace}.start|compatible|agree|marry|divorce <player_hero_id> <npc_hero_id>. " +
                "Only start, compatible, agree, marry, and divorce require the server console. " +
                "Divorce does not restore pre-marriage clan or party changes.");
        }
    }

    public sealed class RomanceStatusCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "status";

        public string Description => "Reports romance state for a player and NPC.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".status";
            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                    return Failed(error);

                var state = Romance.GetRomanticState(playerHero, targetHero);
                return Succeeded(state == null
                    ? $"No romance state exists between {FormatHero(playerHero, objectManager)} and {FormatHero(targetHero, objectManager)}."
                    : $"{FormatState(state, objectManager)}; " +
                      $"spouses={playerHero.Spouse?.StringId ?? "none"}/{targetHero.Spouse?.StringId ?? "none"}");
            });
        }
    }

    public sealed class RomanceStartCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "start";

        public string Description => "Starts courtship between a player and NPC.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".start";
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (!TryGetRomanceAuthority(out var romanceAuthority, out error))
                    return Failed(error);
                if (!romanceAuthority.TryValidateStateChange(
                        playerHero,
                        targetHero,
                        Romance.RomanceLevelEnum.CourtshipStarted,
                        out error))
                    return Failed(error);

                ChangeRomanticStateAction.Apply(
                    playerHero,
                    targetHero,
                    Romance.RomanceLevelEnum.CourtshipStarted);
                return Succeeded($"Changed romance between {playerHero.Name} and {targetHero.Name} to {Romance.RomanceLevelEnum.CourtshipStarted}.");
            });
        }
    }

    public sealed class RomanceCompatibleCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "compatible";

        public string Description => "Marks a romance pair as compatible.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".compatible";
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (!TryGetRomanceAuthority(out var romanceAuthority, out error))
                    return Failed(error);
                if (!romanceAuthority.TryValidateStateChange(
                        playerHero,
                        targetHero,
                        Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible,
                        out error))
                    return Failed(error);

                ChangeRomanticStateAction.Apply(
                    playerHero,
                    targetHero,
                    Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible);
                return Succeeded($"Changed romance between {playerHero.Name} and {targetHero.Name} to {Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible}.");
            });
        }
    }

    public sealed class RomanceAgreeCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "agree";

        public string Description => "Marks a romance pair as agreed on marriage.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".agree";
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (!TryGetRomanceAuthority(out var romanceAuthority, out error))
                    return Failed(error);
                if (!romanceAuthority.TryValidateStateChange(
                        playerHero,
                        targetHero,
                        Romance.RomanceLevelEnum.CoupleAgreedOnMarriage,
                        out error))
                    return Failed(error);

                ChangeRomanticStateAction.Apply(
                    playerHero,
                    targetHero,
                    Romance.RomanceLevelEnum.CoupleAgreedOnMarriage);
                return Succeeded($"Changed romance between {playerHero.Name} and {targetHero.Name} to {Romance.RomanceLevelEnum.CoupleAgreedOnMarriage}.");
            });
        }
    }

    public sealed class RomanceMarryCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "marry";

        public string Description => "Marries a player hero and NPC hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".marry";
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (!TryGetRomanceAuthority(out var romanceAuthority, out error))
                    return Failed(error);
                if (!romanceAuthority.TryValidateMarriage(playerHero, targetHero, out error))
                    return Failed(error);

                MarriageAction.Apply(playerHero, targetHero);
                return playerHero.Spouse == targetHero && targetHero.Spouse == playerHero
                    ? Succeeded($"Married {playerHero.Name} to {targetHero.Name}.")
                    : Failed($"Marriage between {playerHero.Name} and {targetHero.Name} did not complete.");
            });
        }
    }

    public sealed class RomanceDivorceCoopCommand : ICoopCommand
    {
        public string Prefix => CommandNamespace;

        public string Name => "divorce";

        public string Description => "Divorces a player hero and NPC hero.";

        public IExpectedArgs[] ExpectedArgs { get; } = CreatePairArguments();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            const string command = CommandNamespace + ".divorce";
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            return RunOnGameThread(command, () =>
            {
                if (!TryGetPlayerNpcPair(args, out var playerHero, out var targetHero, out string error))
                    return Failed(error);
                if (playerHero.Spouse != targetHero || targetHero.Spouse != playerHero)
                    return Failed($"{playerHero.Name} and {targetHero.Name} are not married to each other.");

                playerHero.Spouse = null;
                ChangeRomanticStateAction.Apply(playerHero, targetHero, Romance.RomanceLevelEnum.Ended);

                return playerHero.Spouse == null &&
                       targetHero.Spouse == null &&
                       Romance.GetRomanticLevel(playerHero, targetHero) == Romance.RomanceLevelEnum.Ended
                    ? Succeeded($"Divorced {playerHero.Name} and {targetHero.Name}.")
                    : Failed($"Divorce between {playerHero.Name} and {targetHero.Name} did not complete.");
            });
        }
    }

    private static IExpectedArgs[] CreatePairArguments() => new IExpectedArgs[]
    {
        new ExpectedArgs("player_hero_id", "The registered player hero id."),
        new ExpectedArgs("npc_hero_id", "The registered NPC hero id."),
    };

    private static bool TryGetPlayerNpcPair(
        IReadOnlyList<string> args,
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

    private static CoopCommandResult RunOnGameThread(string command, Func<CoopCommandResult> action)
    {
        CoopCommandResult result = Failed($"{command} did not complete.");
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

    private static string FormatState(Romance.RomanticState state, IObjectManager objectManager) =>
        $"{FormatHero(state.Person1, objectManager)} <-> {FormatHero(state.Person2, objectManager)}: " +
        $"{state.Level}, progress={state.ProgressToNextLevel}, lastVisit={state.LastVisit}, persuasion={state.ScoreFromPersuasion}";

    private static string FormatHero(Hero hero, IObjectManager objectManager)
    {
        if (hero == null) return "<missing hero>";

        return objectManager.TryGetId(hero, out var heroId)
            ? $"{heroId} ({hero.Name})"
            : $"<unregistered> ({hero.Name})";
    }
}
