using Common;
using GameInterface.Services.Settlements.Audit;
using System.Collections.Generic;

namespace GameInterface.Services.Settlements.Commands;
internal class SettlementAuditorCommand
{
    public static string Audit(List<string> args)
    {

        if(ModInformation.IsServer)
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
