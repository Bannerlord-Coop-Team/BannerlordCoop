using GameInterface.Services.Settlements.Audit;
using Serilog;
using System;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Settlements.Commands;
internal class SettlementAuditorCommand
{
    [CommandLineArgumentFunction("audit", "coop.debug.settlements")]
    public static string Audit(List<string> args)
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            return $"Unable to resolve {typeof(IGameInterfaceConfig)}\n"
                    + $"Callstack: {Environment.StackTrace}";
        }

        if (config.IsServer)
        {
            return "The Settlement Auditor can only be called by the client";
        }
        if (ContainerProvider.TryResolve<SettlementAuditor>(out var auditor) == false)
        {
            return $"Unable to get {nameof(SettlementAuditor)}";
        }

        return auditor.Audit();
    }
}
