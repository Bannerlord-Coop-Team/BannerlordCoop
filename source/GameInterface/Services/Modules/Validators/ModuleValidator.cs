using GameInterface.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Modules.Validators;

/// <summary>
/// Validates and compares the modules of the clients with the modules of the server. 
/// </summary>
public interface IModuleValidator
{
    /// <summary>
    /// Compares two lists of <see cref="ModuleInfo"/>, regardless of the order or sorting of the two lists. The server list specifies what the client list must fulfil. <para/>
    /// Checks that the game versions match (changeset aside), that the client has no DLC enabled, and that the client runs every server community
    /// module with the same version. Client-only community modules must be in the mod-config whitelist, unless it is turned off.
    /// Official non-DLC modules and the DedicatedServer.* host modules are exempt.
    /// </summary>
    /// <param name="serverModules">The server modules</param>
    /// <param name="clientModules">The client modules</param>
    /// <param name="displayNames">Optional display name per module id, only used to make the reason readable.</param>
    /// <returns>Null if the validation was successful, otherwise a reason for the failure of the validation.</returns>
    public bool Validate(IEnumerable<ModuleInfo> serverModules, IEnumerable<ModuleInfo> clientModules, out string error, IReadOnlyDictionary<string, string> displayNames = null);

    /// <summary>
    /// Ensures none of the given modules is official optional content (DLC). Coop does not support
    /// DLC, so it must be disabled on both the server and all connecting clients.
    /// </summary>
    /// <param name="modules">The modules to check.</param>
    /// <param name="error">Null if no DLC was enabled, otherwise a reason naming the enabled DLC.</param>
    /// <returns>True if no DLC is enabled, otherwise false.</returns>
    public bool ValidateNoDlc(IEnumerable<ModuleInfo> modules, out string error);
}

/// <inheritdoc href="IModuleValidator" />
public class ModuleValidator : IModuleValidator
{
    private readonly IModConfig modConfig;

    public ModuleValidator(IModConfig modConfig)
    {
        this.modConfig = modConfig;
    }

    public bool Validate(IEnumerable<ModuleInfo> serverModules, IEnumerable<ModuleInfo> clientModules, out string error, IReadOnlyDictionary<string, string> displayNames = null)
    {
        if (!ValidateGameVersion(serverModules, clientModules, out error))
        {
            return false;
        }

        if (!ValidateNoDlc(clientModules, displayNames, out error))
        {
            return false;
        }

        serverModules = serverModules.Where(ShouldValidateModule).ToList();
        clientModules = clientModules.Where(ShouldValidateModule).ToList();

        // Every mismatch is reported at once, otherwise a joiner fixes one module per attempt.
        var errors = new List<string>();

        foreach (var serverModule in serverModules)
        {
            var clientModule = clientModules.FirstOrDefault(module => module.Id == serverModule.Id);

            if (clientModule.Id == null)
            {
                errors.Add($"To join the server the module {Describe(serverModule, displayNames)} with version '{serverModule.Version.ToString()}' is required.");
            }
            else if (!serverModule.Version.IsSame(clientModule.Version, true))
            {
                errors.Add($"Wrong version of module {Describe(serverModule, displayNames)} detected. Server uses '{serverModule.Version.ToString()}', client uses '{clientModule.Version.ToString()}'.");
            }
        }

        var modulesOnlyClientSide = clientModules.Where(clientModule =>
            serverModules.All(serverModule => serverModule.Id != clientModule.Id) &&
            !IsAllowedClientOnly(clientModule));

        errors.AddRange(modulesOnlyClientSide.Select(clientModule =>
            $"Server does not support module {Describe(clientModule, displayNames)}."));

        if (errors.Any())
        {
            error = string.Join(Environment.NewLine, errors);
            return false;
        }

        error = null;
        return true;
    }

    // A module only the client runs is allowed when the whitelist is off or names it.
    private bool IsAllowedClientOnly(ModuleInfo module)
    {
        var config = modConfig.Data;
        if (config.ModWhitelistEnabled == false) return true;
        if (config.ModWhitelist == null) return false;

        return config.ModWhitelist.Any(id => string.Equals(id?.Trim(), module.Id, StringComparison.OrdinalIgnoreCase));
    }

    // 'Calradia Expanded' (CalradiaExpanded): players know the name, the id is what the files use.
    // The Coop line stays exact because the server matches on UnsupportedCoopModuleReason.
    private static string Describe(ModuleInfo module, IReadOnlyDictionary<string, string> displayNames)
    {
        string name = null;
        if (module.Id != null) displayNames?.TryGetValue(module.Id, out name);

        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, module.Id, StringComparison.Ordinal) ||
            string.Equals(module.Id, "Coop", StringComparison.Ordinal))
        {
            return $"'{module.Id}'";
        }

        return $"'{name}' ({module.Id})";
    }

    private static bool ShouldValidateModule(ModuleInfo module)
    {
        if (module.IsOfficial && !module.IsDlc)
        {
            return false;
        }

        if (module.Id != null && module.Id.StartsWith("DedicatedServer.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public bool ValidateNoDlc(IEnumerable<ModuleInfo> modules, out string error) =>
        ValidateNoDlc(modules, null, out error);

    private static bool ValidateNoDlc(IEnumerable<ModuleInfo> modules, IReadOnlyDictionary<string, string> displayNames, out string error)
    {
        var dlcModules = (modules ?? Enumerable.Empty<ModuleInfo>()).Where(module => module.IsDlc).ToList();

        if (dlcModules.Any())
        {
            error = "DLC is not supported. Please disable the following module(s): " +
                string.Join(", ", dlcModules.Select(module => Describe(module, displayNames))) + ".";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Compares the game version (the version of the official module) of the server and the client.
    /// </summary>
    private static bool ValidateGameVersion(IEnumerable<ModuleInfo> serverModules, IEnumerable<ModuleInfo> clientModules, out string error)
    {
        var serverGameModule = serverModules.FirstOrDefault(module => module.IsOfficial);
        var clientGameModule = clientModules.FirstOrDefault(module => module.IsOfficial);

        // checkChangeSet: false — a dedicated-server distribution and the Steam
        // client are different builds (changesets) of the same game version.
        if (!serverGameModule.Version.IsSame(clientGameModule.Version, false))
        {
            error = $"Wrong game version detected. Server uses '{serverGameModule.Version.ToString()}', client uses '{clientGameModule.Version.ToString()}'.";
            return false;
        }

        error = null;
        return true;
    }
}
