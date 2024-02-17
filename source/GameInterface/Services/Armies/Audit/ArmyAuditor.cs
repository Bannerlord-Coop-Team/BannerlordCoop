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
internal class ArmyAuditor : IAuditor
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyAuditor>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ArmyRegistry armyRegistry;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private TaskCompletionSource<string> tcs;

    public ArmyAuditor(
        IMessageBroker messageBroker,
        INetwork network,
        ArmyRegistry armyRegistry,
        IObjectManager objectManager,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.armyRegistry = armyRegistry;
        this.objectManager = objectManager;
        this.configuration = configuration;
        messageBroker.Subscribe<RequestArmyAudit>(Handle_Request);
        messageBroker.Subscribe<ArmyAuditResponse>(Handle_Response);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestArmyAudit>(Handle_Request);
        messageBroker.Unsubscribe<ArmyAuditResponse>(Handle_Response);
    }

    private void Handle_Response(MessagePayload<ArmyAuditResponse> payload)
    {
        var stringBuilder = new StringBuilder();
        var auditDatas = payload.What.Data;

        stringBuilder.AppendLine("Server Audit Result:");
        stringBuilder.AppendLine(payload.What.ServerAuditResult);

        stringBuilder.AppendLine("Client Audit Result:");
        stringBuilder.AppendLine(AuditData(auditDatas));

        tcs.SetResult(stringBuilder.ToString());
    }

    private void Handle_Request(MessagePayload<RequestArmyAudit> payload)
    {
        var serverAuditResult = AuditData(payload.What.Data);
        var response = new ArmyAuditResponse(GetAuditData(), serverAuditResult);
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

        var request = new RequestArmyAudit(GetAuditData());

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

    private IEnumerable<Army> GetArmys()
    {
        return armyRegistry.Objects.Values;
    }

    private ArmyAuditData[] GetAuditData()
    {
        return GetArmys().Select(h => new ArmyAuditData(h)).ToArray();
    }

    private string AuditData(ArmyAuditData[] dataToAudit)
    {
        var stringBuilder = new StringBuilder();

        var errorCount = 0;

        var ArmyCount = GetArmys().Count();
        stringBuilder.AppendLine($"Auditing {ArmyCount} objects");

        if (ArmyCount != dataToAudit.Length)
        {
            Logger.Error("Army count mismatch: {ArmyCount} != {dataToAudit.Length}", ArmyCount, dataToAudit.Length);
            stringBuilder.AppendLine($"Army count mismatch: {ArmyCount} != {dataToAudit.Length}");
        }

        foreach (var audit in dataToAudit)
        {
            if (objectManager.TryGetObject<Army>(audit.StringId, out var _) == false)
            {
                Logger.Error("Army {name} not found in {objectManager}", audit.Name, nameof(IObjectManager));
                stringBuilder.AppendLine($"Army {audit.Name} not found in {nameof(IObjectManager)}");
                errorCount++;
            }
        }

        stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Length} objects");

        return stringBuilder.ToString();
    }
}
