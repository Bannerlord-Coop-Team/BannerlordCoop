using Common.Commands;
using GameInterface.Utils.Commands;

namespace GameInterface.Services.Settlements.Commands;

public interface IAuditCoopCommand : ICoopCommand
{
}

public sealed class AuditCoopCommand : LegacyCoopCommand, IAuditCoopCommand
{
    public AuditCoopCommand()
        : base(
            "coop.debug.settlements",
            "audit",
            "Audits the relevant state for co-op debugging.",
            System.Array.Empty<IExpectedArgs>(),
            SettlementAuditorCommand.Audit)
    {
    }
}
