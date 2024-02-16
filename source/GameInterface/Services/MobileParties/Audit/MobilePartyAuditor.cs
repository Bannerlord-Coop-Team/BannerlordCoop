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
internal class MobilePartyAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly MobilePartyRegistry mobilePartyRegistry;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private TaskCompletionSource<string> tcs;

    public MobilePartyAuditor(
        IMessageBroker messageBroker,
        INetwork network,
        MobilePartyRegistry mobilePartyRegistry,
        IObjectManager objectManager,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.mobilePartyRegistry = mobilePartyRegistry;
        this.objectManager = objectManager;
        this.configuration = configuration;
        messageBroker.Subscribe<RequestMobilePartyAudit>(Handle_Request);
        messageBroker.Subscribe<MobilePartyAuditResponse>(Handle_Response);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestMobilePartyAudit>(Handle_Request);
        messageBroker.Unsubscribe<MobilePartyAuditResponse>(Handle_Response);
    }

    private void Handle_Response(MessagePayload<MobilePartyAuditResponse> payload)
    {
        var stringBuilder = new StringBuilder();
        var auditDatas = payload.What.Data;

        stringBuilder.AppendLine("Server Audit Result:");
        stringBuilder.AppendLine(payload.What.ServerAuditResult);

        stringBuilder.AppendLine("Client Audit Result:");
        stringBuilder.AppendLine(AuditData(auditDatas));

        tcs.SetResult(stringBuilder.ToString());
    }

    private void Handle_Request(MessagePayload<RequestMobilePartyAudit> payload)
    {
        var serverAuditResult = AuditData(payload.What.Data);
        var response = new MobilePartyAuditResponse(GetAuditData(), serverAuditResult);
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

        var request = new RequestMobilePartyAudit(GetAuditData());

        network.SendAll(request);

        try
        {
            tcs.Task.Wait();
            return tcs.Task.Result;
        }
        catch (AggregateException ex)
        {
            if (ex.InnerException is TaskCanceledException == false) throw ex;

            var errorMsg = "Audit timed out";
            Logger.Error(errorMsg);
            return errorMsg;
        }
    }

    private IEnumerable<MobileParty> GetMobilePartys()
    {
        return Campaign.Current.CampaignObjectManager.MobileParties;
    }

    private MobilePartyAuditData[] GetAuditData()
    {
        return GetMobilePartys().Select(h => new MobilePartyAuditData(h)).ToArray();
    }

    private string AuditData(MobilePartyAuditData[] dataToAudit)
    {
        var stringBuilder = new StringBuilder();

        var errorCount = 0;

        var MobilePartyCount = GetMobilePartys().Count();
        stringBuilder.AppendLine($"Auditing {MobilePartyCount} objects");

        if (MobilePartyCount != dataToAudit.Length)
        {
            Logger.Error("MobileParty count mismatch: {MobilePartyCount} != {dataToAudit.Length}", MobilePartyCount, dataToAudit.Length);
            stringBuilder.AppendLine($"MobileParty count mismatch: {MobilePartyCount} != {dataToAudit.Length}");
        }

        foreach (var audit in dataToAudit)
        {
            if (objectManager.TryGetObject<MobileParty>(audit.StringId, out var _) == false)
            {
                Logger.Error("MobileParty {name} not found in {objectManager}", audit.Name, nameof(IObjectManager));
                stringBuilder.AppendLine($"MobileParty {audit.Name} not found in {nameof(IObjectManager)}");
                errorCount++;
            }
        }

        stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Length} objects");

        return stringBuilder.ToString();
    }
}
