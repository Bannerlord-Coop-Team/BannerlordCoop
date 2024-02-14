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
internal class HeroAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly HeroRegistry heroRegistry;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private TaskCompletionSource<string> tcs;

    public HeroAuditor(
        IMessageBroker messageBroker,
        INetwork network,
        HeroRegistry heroRegistry,
        IObjectManager objectManager,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.heroRegistry = heroRegistry;
        this.objectManager = objectManager;
        this.configuration = configuration;
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

        stringBuilder.AppendLine("Server Audit Result:");
        stringBuilder.AppendLine(payload.What.ServerAuditResult);

        stringBuilder.AppendLine("Client Audit Result:");
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

        var cts = new CancellationTokenSource(configuration.AuditTimeout);
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

    private IEnumerable<Hero> GetHeros()
    {
        return Campaign.Current.CampaignObjectManager.GetAllHeroes();
    }

    private HeroAuditData[] GetAuditData()
    {
        return GetHeros().Select(h => new HeroAuditData(h)).ToArray();
    }

    private string AuditData(HeroAuditData[] dataToAudit)
    {
        var stringBuilder = new StringBuilder();

        var errorCount = 0;

        var heroCount = GetHeros().Count();
        stringBuilder.AppendLine($"Auditing {heroCount} objects");

        if (heroCount != dataToAudit.Length)
        {
            Logger.Error("Hero count mismatch: {heroCount} != {dataToAudit.Length}", heroCount, dataToAudit.Length);
            stringBuilder.AppendLine($"Hero count mismatch: {heroCount} != {dataToAudit.Length}");
        }
        
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
