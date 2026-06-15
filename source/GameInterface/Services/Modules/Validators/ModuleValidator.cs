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
    /// Checks whether the client list contains all the server's modules, whether the versions are the same and whether the client only uses modules that the server also uses.
    /// </summary>
    /// <param name="serverModules">The server modules</param>
    /// <param name="clientModules">The client modules</param>
    /// <returns>Null if the validation was successful, otherwise a reason for the failure of the validation.</returns>
    public bool Validate(IEnumerable<ModuleInfo> serverModules, IEnumerable<ModuleInfo> clientModules, out string error);

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
    public bool Validate(IEnumerable<ModuleInfo> serverModules, IEnumerable<ModuleInfo> clientModules, out string error)
    {
        if (!ValidateGameVersion(serverModules, clientModules, out error))
        {
            return false;
        }

        if (!ValidateNoDlc(clientModules, out error))
        {
            return false;
        }

        foreach (var serverModule in serverModules)
        {
            var clientModule = clientModules.FirstOrDefault(module => module.Id == serverModule.Id);

            if (clientModule.Id == null)
            {
                error = $"To join the server the module '{serverModule.Id}' with version '{serverModule.Version.ToString()}' is required.";
                return false;
            }
            else if (!serverModule.Version.IsSame(clientModule.Version, true))
            {
                error = $"Wrong version of module '{serverModule.Id}' detected. Server uses '{serverModule.Version.ToString()}', client uses '{clientModule.Version.ToString()}'.";
                return false;
                    
            }
        }

        var modulesOnlyClientSide = clientModules.Where(clientModule =>
            serverModules.All(serverModule => serverModule.Id != clientModule.Id)).ToList();

        if (modulesOnlyClientSide.Any())
        {
            error = string.Join(Environment.NewLine,
                modulesOnlyClientSide.Select(clientModule =>
                    $"Server does not support module '{clientModule.Id}'."));

            return false;
        }

        error = null;
        return true;
    }

    public bool ValidateNoDlc(IEnumerable<ModuleInfo> modules, out string error)
    {
        var dlcModules = (modules ?? Enumerable.Empty<ModuleInfo>()).Where(module => module.IsDlc).ToList();

        if (dlcModules.Any())
        {
            error = "DLC is not supported. Please disable the following module(s): " +
                string.Join(", ", dlcModules.Select(module => $"'{module.Id}'")) + ".";
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

        if (!serverGameModule.Version.IsSame(clientGameModule.Version, true))
        {
            error = $"Wrong game version detected. Server uses '{serverGameModule.Version.ToString()}', client uses '{clientGameModule.Version.ToString()}'.";
            return false;
        }

        error = null;
        return true;
    }
}