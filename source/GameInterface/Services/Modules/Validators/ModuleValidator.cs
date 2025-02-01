using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Modules.Validators
{
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
        public string Validate(List<ModuleInfo> serverModules, List<ModuleInfo> clientModules);
    }

    /// <inheritdoc href="IModuleValidator" />
    public class ModuleValidator : IModuleValidator
    {
        public string Validate(List<ModuleInfo> serverModules, List<ModuleInfo> clientModules)
        {
            foreach (var serverModule in serverModules)
            {
                var clientModule = clientModules.FirstOrDefault(module => module.Id == serverModule.Id);

                if (clientModule.Id == null)
                {
                    return
                        $"To join the server the module '{serverModule.Id}' with version '{serverModule.Version.ToString()}' is required.";
                }
                else if (!serverModule.Version.IsSame(clientModule.Version, true))
                {
                    return
                        $"Wrong version of module '{serverModule.Id}' detected. Server uses '{serverModule.Version.ToString()}', client uses '{clientModule.Version.ToString()}'.";
                }
            }

            var modulesOnlyClientSide = clientModules.Where(clientModule =>
                serverModules.All(serverModule => serverModule.Id != clientModule.Id)).ToList();

            if (modulesOnlyClientSide.Any())
            {
                return string.Join(Environment.NewLine,
                    modulesOnlyClientSide.Select(clientModule =>
                        $"Server does not support module '{clientModule.Id}'."));
            }

            return null;
        }
    }
}