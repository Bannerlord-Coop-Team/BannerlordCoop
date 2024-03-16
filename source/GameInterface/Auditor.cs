using Common.Audit;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Audit;
using GameInterface.Services.Armies;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using TaleWorlds.CampaignSystem.Party;
using System.Linq;

namespace GameInterface
{
    public interface IAuditResponse : IEvent
    {
        public string ServerAuditResult { get; }
        public IEnumerable<IAuditData> Data { get; }
    }
    public interface IAuditRequest : ICommand
    {
        public IEnumerable<IAuditData> Data { get; }
    }
    public interface IAuditData
    {
        public string Name { get; }
        public string StringId { get; }
    }
    public abstract class Auditor<Request, Response, AuditingType, AuditData, LoggerType> : IAuditor 
        where Request : IAuditRequest, new()
        where Response : IAuditResponse, new()
        where AuditingType : class
        where AuditData : IAuditData, new()

    {
        private static readonly ILogger Logger = LogManager.GetLogger<LoggerType>();

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly INetworkConfiguration configuration;
        private TaskCompletionSource<string> tcs;

        public Auditor(
            IMessageBroker messageBroker,
            INetwork network,
            IObjectManager objectManager,
            INetworkConfiguration configuration)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            this.configuration = configuration;
            messageBroker.Subscribe<Request>(Handle_Request);
            messageBroker.Subscribe<Response>(Handle_Response);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<Request>(Handle_Request);
            messageBroker.Unsubscribe<Response>(Handle_Response);
        }

        private void Handle_Response(MessagePayload<Response> payload)
        {
            var stringBuilder = new StringBuilder();
            var auditDatas = payload.What.Data;

            stringBuilder.AppendLine("Server Audit Result:");
            stringBuilder.AppendLine(payload.What.ServerAuditResult);

            stringBuilder.AppendLine("Client Audit Result:");
            stringBuilder.AppendLine(DoAuditData(auditDatas));

            tcs.SetResult(stringBuilder.ToString());
        }

        private void Handle_Request(MessagePayload<Request> payload)
        {
            var serverAuditResult = DoAuditData(payload.What.Data);
            var response = CreateRequstInstance(GetAuditData(), serverAuditResult);
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

            var request = new Request();

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
        public abstract IEnumerable<AuditingType> Objects {get;}
        public abstract IEnumerable<AuditData> GetAuditData();
        public virtual string DoAuditData(IEnumerable<IAuditData> dataToAudit)
        {
            var typeName = typeof(AuditingType).Name;
            var stringBuilder = new StringBuilder();

            var errorCount = 0;
            var objectsCount = Objects.Count();
            stringBuilder.AppendLine($"Auditing {objectsCount} objects");

            if (objectsCount != dataToAudit.Count())
            {
                Logger.Error($"{typeName} count mismatch: {objectsCount} != {dataToAudit.Count()}");
                stringBuilder.AppendLine($"{typeName} count mismatch: {objectsCount} != {dataToAudit.Count()}");
            }

            foreach (var audit in dataToAudit)
            {
                if (objectManager.TryGetObject<AuditingType>(audit.StringId, out var _) == false)
                {
                    Logger.Error($"{typeName} {audit.Name} not found in {objectManager.GetType().Name}");
                    stringBuilder.AppendLine($"MobileParty {audit} not found in {nameof(IObjectManager)}");
                    errorCount++;
                }
            }

            stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Count()} objects");

            return stringBuilder.ToString();
        }
        public abstract Request CreateRequstInstance(IEnumerable<AuditData> par1, string par2);
    }
}
