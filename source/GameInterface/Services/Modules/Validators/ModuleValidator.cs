using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Modules.Validators;

public class ModuleValidator : IModuleValidator
{
    public string Validate(List<ModuleInfo> serverModules, List<ModuleInfo> clientModules)
    {
        foreach (var serverModule in serverModules)
        {
            var clientModule = clientModules.FirstOrDefault(module => module.Id == serverModule.Id);

            if (clientModule.Id == null)
            {
                return $"To join the server the module '{serverModule.Id}' with version '{serverModule.Version.ToString()}' is required.";
            }
            else if (!serverModule.Version.IsSame(clientModule.Version, true))
            {
                return $"Wrong version of module '{serverModule.Id}' detected. Server uses '{serverModule.Version.ToString()}', client uses '{clientModule.Version.ToString()}'.";
            }
        }
        
        var modulesOnlyClientSide = clientModules.Where(clientModule => serverModules.All(serverModule => serverModule.Id != clientModule.Id)).ToList();

        if (modulesOnlyClientSide.Any())
        {
            return string.Join(Environment.NewLine, modulesOnlyClientSide.Select(clientModule => $"Server does not support module '{clientModule.Id}'."));
        }

        return null;
    }
}