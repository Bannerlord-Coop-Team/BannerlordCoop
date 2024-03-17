using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Registry;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Audit;
internal class HeroAuditor : Auditor<RequestHeroAudit, HeroAuditResponse, Hero, HeroAuditData, HeroAuditor>
{
    public HeroAuditor(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration)
    {
    }

    public override IEnumerable<Hero> Objects => Campaign.Current.CampaignObjectManager.GetAllHeroes();

    public override RequestHeroAudit CreateRequestInstance(IEnumerable<HeroAuditData> par1)
    {
        return new RequestHeroAudit(par1.ToArray());
    }
    public override HeroAuditResponse CreateResponseInstance(IEnumerable<HeroAuditData> par1, string par2)
    {
        return new HeroAuditResponse(par1.ToArray(), par2);
    }
    public override IEnumerable<HeroAuditData> GetAuditData()
    {
        return Objects.Select(h => new HeroAuditData(h)).ToArray();
    }
}
