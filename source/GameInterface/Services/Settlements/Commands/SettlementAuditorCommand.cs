using Common.Commands;
using Common;
using GameInterface.Services.Settlements.Audit;
using System.Collections.Generic;
namespace GameInterface.Services.Settlements.Commands;
internal class SettlementAuditorCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");



    public sealed class AuditCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.settlements";

        public string Name => "audit";

        public string Description => "Audits the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if(ModInformation.IsServer)
            {
                return Failed("The Settlement Auditor can only be called by the client");
            }
            if (ContainerProvider.TryResolve<SettlementAuditor>(out var auditor) == false)
            {
                return Failed($"Unable to get {nameof(SettlementAuditor)}");
            }

            return Succeeded(auditor.Audit());

        }
    }
}
