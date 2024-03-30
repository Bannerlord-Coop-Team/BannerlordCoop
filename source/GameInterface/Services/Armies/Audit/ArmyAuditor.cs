using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Audit;

/// <summary>
/// Auditor for <see cref="Army"/> objects
/// </summary>
internal class ArmyAuditor : AuditorBase<RequestArmyAudit, ArmyAuditResponse, ArmyAuditData, Army>
{
    public ArmyAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration)
    {
    }
    public override IEnumerable<Army> Objects => Campaign.Current?.Kingdoms?.SelectMany(x=>x.Armies) ?? Array.Empty<Army>();

    public override IEnumerable<ArmyAuditData> GetAuditData()
    {
        return Objects.Select(h => new ArmyAuditData(h)).ToArray();
    }
}
