using Common;
using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;


namespace GameInterface.Services.SettlementComponents.Audit;

/// <summary>
/// Auditor for <see cref="SettlementComponent"/> objects
/// </summary>
internal class SettlementComponentAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementComponentAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private TaskCompletionSource<string> tcs;

    public SettlementComponentAuditor(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;
        messageBroker.Subscribe<RequestSettlementComponentAudit>(Handle_Request);
        messageBroker.Subscribe<SettlementComponentAuditResponse>(Handle_Response);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestSettlementComponentAudit>(Handle_Request);
        messageBroker.Unsubscribe<SettlementComponentAuditResponse>(Handle_Response);
    }

    private void Handle_Response(MessagePayload<SettlementComponentAuditResponse> payload)
    {
        var stringBuilder = new StringBuilder();
        var auditDatas = payload.What.Data;

        stringBuilder.AppendLine("Server Audit Response:");
        stringBuilder.AppendLine(payload.What.ServerAuditResult);

        stringBuilder.AppendLine("Client audit Response:");
        stringBuilder.AppendLine(AuditData(auditDatas));

        tcs.SetResult(stringBuilder.ToString());
    }

    private void Handle_Request(MessagePayload<RequestSettlementComponentAudit> payload)
    {
        var serverAuditResult = AuditData(payload.What.Data);
        var response = new SettlementComponentAuditResponse(GetAuditData(), serverAuditResult);
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

        var request = new RequestSettlementComponentAudit(GetAuditData());

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

    private IEnumerable<SettlementComponent> GetSettlementComponents()
    {
        return Settlement.All.Select(x => x.SettlementComponent);
    }

    private IEnumerable<SettlementComponentAuditData> GetAuditData()
    {
        return GetSettlementComponents().Select(settlementComponent =>
            {
                if (!objectManager.TryGetId(settlementComponent, out var networkId))
                    return null;

                return new SettlementComponentAuditData(
                    networkId,
                    settlementComponent.StringId,
                    settlementComponent.Name?.ToString()
                );
            })
            .Where(x => x is not null);
    }

    private string AuditData(SettlementComponentAuditData[] dataToAudit)
    {
        var stringBuilder = new StringBuilder();

        var errorCount = 0;

        var SettlementComponentCount = GetSettlementComponents().Count();
        stringBuilder.AppendLine($"Auditing {SettlementComponentCount} objects");

        if (SettlementComponentCount != dataToAudit.Length)
        {
            Logger.Error("SettlementComponent count mismatch: {SettlementComponentCount} != {dataToAudit.Length}", SettlementComponentCount, dataToAudit.Length);
            stringBuilder.AppendLine($"SettlementComponent count mismatch: {SettlementComponentCount} != {dataToAudit.Length}");
        }

        foreach (var audit in dataToAudit)
        {
            if (objectManager.TryGetObject<SettlementComponent>(audit.StringId, out var _) == false)
            {
                Logger.Error("SettlementComponent {name} not found in {objectManager}", audit.Name, nameof(IObjectManager));
                stringBuilder.AppendLine($"SettlementComponent {audit.Name} not found in {nameof(IObjectManager)}");
                errorCount++;
            }
        }

        stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Length} objects");

        return stringBuilder.ToString();
    }
}

