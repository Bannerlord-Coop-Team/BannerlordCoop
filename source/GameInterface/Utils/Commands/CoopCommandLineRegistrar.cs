using Common.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.Utils.Commands;

public interface ICoopCommandLineRegistrar : IDisposable
{
}

public sealed class CoopCommandLineRegistrar : ICoopCommandLineRegistrar
{
    private readonly ICoopCommandRegistry commandRegistry;
    private readonly ICoopCommandArgsFactory argsFactory;
    private readonly IRglCommandLineRegistry rglCommandLineRegistry;
    private readonly IDictionary gameCommands;
    private readonly Type gameCommandType;
    private readonly FieldInfo gameCommandDelegateField;
    private readonly Dictionary<string, Registration> registrations =
        new Dictionary<string, Registration>(StringComparer.Ordinal);
    private bool disposed;

    public CoopCommandLineRegistrar(
        ICoopCommandRegistry commandRegistry,
        ICoopCommandArgsFactory argsFactory,
        IRglCommandLineRegistry rglCommandLineRegistry)
    {
        if (commandRegistry == null) throw new ArgumentNullException(nameof(commandRegistry));
        if (argsFactory == null) throw new ArgumentNullException(nameof(argsFactory));
        if (rglCommandLineRegistry == null) throw new ArgumentNullException(nameof(rglCommandLineRegistry));

        this.commandRegistry = commandRegistry;
        this.argsFactory = argsFactory;
        this.rglCommandLineRegistry = rglCommandLineRegistry;

        // Publicizing TaleWorlds.Library here produces duplicate InformationManager members.
        FieldInfo allFunctionsField = typeof(CommandLineFunctionality).GetField(
            "AllFunctions",
            BindingFlags.NonPublic | BindingFlags.Static);
        gameCommandType = typeof(CommandLineFunctionality).GetNestedType(
            "CommandLineFunction",
            BindingFlags.NonPublic);
        gameCommandDelegateField = gameCommandType?.GetField(
            "CommandLineFunc",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (allFunctionsField == null ||
            !(allFunctionsField.GetValue(null) is IDictionary registeredGameCommands) ||
            gameCommandType == null ||
            gameCommandDelegateField == null)
        {
            throw new InvalidOperationException("Unable to access the Bannerlord command registry.");
        }

        gameCommands = registeredGameCommands;

        try
        {
            lock (gameCommands.SyncRoot)
            {
                RegisterCommands();
            }
        }
        catch
        {
            disposed = true;
            lock (gameCommands.SyncRoot)
            {
                RemoveRegistrations();
            }
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        lock (gameCommands.SyncRoot)
        {
            RemoveRegistrations();
        }
    }

    private void RegisterCommands()
    {
        foreach (CoopCommandDescriptor descriptor in commandRegistry.Commands)
        {
            RegisterCommand(descriptor.FullName, descriptor.FullName);
        }

        foreach (KeyValuePair<string, string> alias in commandRegistry.LegacyAliases)
        {
            RegisterCommand(alias.Key, alias.Value);
        }
    }

    private void RegisterCommand(string registeredName, string targetFullName)
    {
        object previousRegistration = null;
        if (gameCommands.Contains(registeredName))
        {
            previousRegistration = gameCommands[registeredName];
            // Reconnects and tests may temporarily have overlapping session lifetime scopes.
            if (!TryGetOwningRegistrar(previousRegistration, out _))
                throw new InvalidOperationException($"The command '{registeredName}' is already registered with the game.");
        }

        var invoker = new GameCommandInvoker(this, targetFullName);
        Func<List<string>, string> commandDelegate = invoker.ProcessCommand;
        object gameCommand = Activator.CreateInstance(
            gameCommandType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { commandDelegate },
            null);
        if (gameCommand == null)
            throw new InvalidOperationException($"Unable to create a game registration for '{registeredName}'.");

        gameCommands[registeredName] = gameCommand;
        registrations.Add(registeredName, new Registration(gameCommand, previousRegistration));
        rglCommandLineRegistry.RegisterCommand(registeredName);
    }

    private string ProcessCommand(string fullName, IEnumerable<string> tokens)
    {
        if (!argsFactory.TryFromConsoleTokens(tokens, out ICoopCommandArgs args, out string error))
            return $"Invalid arguments: {error}";

        return commandRegistry.ProcessCommand(fullName, args).Output;
    }

    private void RemoveRegistrations()
    {
        foreach (KeyValuePair<string, Registration> registration in registrations)
        {
            if (!gameCommands.Contains(registration.Key)) continue;

            object currentRegistration = gameCommands[registration.Key];
            if (!ReferenceEquals(currentRegistration, registration.Value.GameCommand))
            {
                PruneDisposedPreviousRegistrations(registration.Key, currentRegistration);
                continue;
            }

            object previousRegistration = FindActivePreviousRegistration(
                registration.Key,
                registration.Value.PreviousRegistration);
            if (previousRegistration == null)
            {
                gameCommands.Remove(registration.Key);
            }
            else
            {
                gameCommands[registration.Key] = previousRegistration;
            }
        }
    }

    private void PruneDisposedPreviousRegistrations(string fullName, object currentRegistration)
    {
        object candidate = currentRegistration;
        while (candidate != null && TryGetOwningRegistrar(candidate, out CoopCommandLineRegistrar owner))
        {
            if (!owner.registrations.TryGetValue(fullName, out Registration registration) ||
                !ReferenceEquals(registration.GameCommand, candidate))
            {
                return;
            }

            object activePreviousRegistration = FindActivePreviousRegistration(
                fullName,
                registration.PreviousRegistration);
            registration.PreviousRegistration = activePreviousRegistration;
            candidate = activePreviousRegistration;
        }
    }

    private object FindActivePreviousRegistration(string fullName, object previousRegistration)
    {
        object candidate = previousRegistration;
        while (candidate != null && TryGetOwningRegistrar(candidate, out CoopCommandLineRegistrar owner))
        {
            if (!owner.disposed) return candidate;

            candidate = owner.GetPreviousRegistration(fullName, candidate);
        }

        return candidate;
    }

    private object GetPreviousRegistration(string fullName, object gameCommand)
    {
        if (registrations.TryGetValue(fullName, out Registration registration) &&
            ReferenceEquals(registration.GameCommand, gameCommand))
        {
            return registration.PreviousRegistration;
        }

        return null;
    }

    private bool TryGetOwningRegistrar(object gameCommand, out CoopCommandLineRegistrar registrar)
    {
        registrar = null;
        if (!(gameCommandDelegateField.GetValue(gameCommand) is Delegate commandDelegate)) return false;

        if (!(commandDelegate.Target is GameCommandInvoker invoker)) return false;

        registrar = invoker.Registrar;
        return true;
    }

    private sealed class GameCommandInvoker
    {
        private readonly string fullName;

        public GameCommandInvoker(CoopCommandLineRegistrar registrar, string fullName)
        {
            Registrar = registrar;
            this.fullName = fullName;
        }

        public CoopCommandLineRegistrar Registrar { get; }

        public string ProcessCommand(List<string> tokens)
        {
            return Registrar.ProcessCommand(fullName, tokens);
        }
    }

    private sealed class Registration
    {
        public Registration(object gameCommand, object previousRegistration)
        {
            GameCommand = gameCommand;
            PreviousRegistration = previousRegistration;
        }

        public object GameCommand { get; }

        public object PreviousRegistration { get; set; }
    }
}
