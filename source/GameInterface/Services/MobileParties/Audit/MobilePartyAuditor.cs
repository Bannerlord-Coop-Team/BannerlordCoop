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
internal class MobilePartyAuditor : Auditor<RequestMobilePartyAudit, MobilePartyAuditResponse, MobileParty, MobilePartyAuditData, MobilePartyAuditor>
{
    public MobilePartyAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration)
    {
    }

    public override IEnumerable<MobileParty> Objects => Campaign.Current.CampaignObjectManager.MobileParties;

    public override RequestMobilePartyAudit CreateRequestInstance(IEnumerable<MobilePartyAuditData> par1)
    {
        return new RequestMobilePartyAudit(par1.ToArray());
    }

    public override MobilePartyAuditResponse CreateResponseInstance(IEnumerable<MobilePartyAuditData> par1, string par2)
    {
        return new MobilePartyAuditResponse(par1.ToArray(), par2);
    }

    public override IEnumerable<MobilePartyAuditData> GetAuditData()
    {
        return Objects.Select(h => new MobilePartyAuditData(h)).ToArray();
    }
}
