using System.Collections.Generic;

namespace GameInterface.Services.Modules.Validators;

public interface IModuleValidator
{
    /// <summary>
    /// Compares two lists of <see cref="ModuleInfo"/>, regardless of the order or sorting of the two lists. The server list specifies what the client list must fulfil. <para/>
    /// Checks whether the client list contains all the server's modules, whether the versions are the same and whether the client only uses modules that the server also uses.
    /// </summary>
    /// <param name="serverModules">The server modules</param>
    /// <param name="clientModules">The client modules</param>
    /// <returns>Null if the validation was successful, otherwise a reason for the failure of the validation.</returns>
    public string Validate(List<ModuleInfo> serverModules, List<ModuleInfo> clientModules);
}