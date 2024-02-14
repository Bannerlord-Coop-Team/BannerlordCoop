using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Audit;
internal class HeroAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly HeroRegistry heroRegistry;
    private readonly IObjectManager objectManager;

    private TaskCompletionSource<string> tcs;

    public HeroAuditor(
        IMessageBroker messageBroker,
        INetwork network,
        HeroRegistry heroRegistry,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroRegistry = heroRegistry;
        this.objectManager = objectManager;
        messageBroker.Subscribe<RequestHeroAudit>(Handle_Request);
        messageBroker.Subscribe<HeroAuditResponse>(Handle_Response);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestHeroAudit>(Handle_Request);
        messageBroker.Unsubscribe<HeroAuditResponse>(Handle_Response);
    }

    private void Handle_Response(MessagePayload<HeroAuditResponse> payload)
    {
        var stringBuilder = new StringBuilder();
        var auditDatas = payload.What.Data;

        stringBuilder.AppendLine(payload.What.ServerAuditResult);
        stringBuilder.AppendLine(AuditData(auditDatas));

        tcs.SetResult(stringBuilder.ToString());
    }

    private void Handle_Request(MessagePayload<RequestHeroAudit> payload)
    {
        var serverAuditResult = AuditData(payload.What.Data);
        var response = new HeroAuditResponse(GetAuditData(), serverAuditResult);
        network.Send((NetPeer)payload.Who, response);
    }

    public string Audit()
    {
        if (ModInformation.IsServer)
        {
            var errorMsg = "Audit is only available client side";
            Logger.Error(errorMsg);
            return errorMsg;
        }

        // TODO move timeout to config
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        tcs = new TaskCompletionSource<string>();

        cts.Token.Register(() =>
        {
            tcs.TrySetCanceled();
        });

        var request = new RequestHeroAudit(GetAuditData());

        network.SendAll(request);

        try
        {
            tcs.Task.Wait();
            return tcs.Task.Result;
        }
        catch(AggregateException ex)
        {
            if (ex.InnerException is TaskCanceledException == false) throw ex;

            var errorMsg = "Audit timed out";
            Logger.Error(errorMsg);
            return errorMsg;
        }
    }

    private HeroAuditData[] GetAuditData()
    {
        var aliveHeros = Campaign.Current.CampaignObjectManager.AliveHeroes;
        var deadHeros = Campaign.Current.CampaignObjectManager.DeadOrDisabledHeroes;
        var heroes = aliveHeros.Concat(deadHeros);

        return heroRegistry.Objects.Values.Select(h => new HeroAuditData(h)).ToArray();
    }

    private string AuditData(HeroAuditData[] dataToAudit)
    {
        var stringBuilder = new StringBuilder();

        var errorCount = 0;

        stringBuilder.AppendLine($"Auditing {dataToAudit.Length} objects");
        foreach (var audit in dataToAudit)
        {
            if (objectManager.TryGetObject<Hero>(audit.StringId, out var _) == false)
            {
                Logger.Error("Hero {name} not found in {objectManager}", audit.Name, nameof(IObjectManager));
                stringBuilder.AppendLine($"Hero {audit.Name} not found in {nameof(IObjectManager)}");
                errorCount++;
            }
        }

        stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Length} objects");

        return stringBuilder.ToString();
    }
}
