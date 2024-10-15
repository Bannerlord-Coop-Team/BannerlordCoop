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
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Audit;
internal class MobilePartyAuditor : AuditorBase<RequestMobilePartyAudit, MobilePartyAuditResponse, MobilePartyAuditData, MobileParty>
{
    public MobilePartyAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration)
    {
    }

    public override IEnumerable<MobileParty> Objects => Campaign.Current.CampaignObjectManager.MobileParties;

    public override IEnumerable<MobilePartyAuditData> GetAuditData()
    {
        return Objects.Select(h => new MobilePartyAuditData(h)).ToArray();
    }
}
