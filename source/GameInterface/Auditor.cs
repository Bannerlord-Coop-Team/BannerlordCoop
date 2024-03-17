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
        public IAuditData[] Data { get; }
    }
    public interface IAuditData
    {
        public string Name { get; }
        public string StringId { get; }
    }
    public abstract class Auditor<Request, Response, AuditingType, AuditData, LoggerType> : IAuditor 
        where Request : IAuditRequest
        where Response : IAuditResponse
        where AuditingType : class
        where AuditData : IAuditData

    {
        protected static readonly ILogger Logger = LogManager.GetLogger<LoggerType>();

        protected readonly IMessageBroker messageBroker;
        protected readonly INetwork network;
        protected readonly IObjectManager objectManager;
        protected readonly INetworkConfiguration configuration;
        protected TaskCompletionSource<string> tcs;

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

        protected virtual void Handle_Response(MessagePayload<Response> payload)
        {
            var stringBuilder = new StringBuilder();
            var auditDatas = payload.What.Data;

            stringBuilder.AppendLine("Server Audit Result:");
            stringBuilder.AppendLine(payload.What.ServerAuditResult);

            stringBuilder.AppendLine("Client Audit Result:");
            stringBuilder.AppendLine(DoAuditData(auditDatas.Cast<AuditData>()));

            tcs.SetResult(stringBuilder.ToString());
        }

        protected virtual void Handle_Request(MessagePayload<Request> payload)
        {
            var serverAuditResult = DoAuditData(payload.What.Data.Cast<AuditData>());
            var response = CreateResponseInstance(GetAuditData(), serverAuditResult);
            network.Send((NetPeer)payload.Who, response);
        }

        public virtual string Audit()
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

            var request = CreateRequestInstance(GetAuditData());

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
        public virtual string DoAuditData(IEnumerable<AuditData> dataToAudit)
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
                if (objectManager.TryGetObject<AuditingType>(audit.StringId, out var obj) == false)
                {
                    Logger.Error($"{typeName} {audit.Name} not found in {objectManager.GetType().Name}");
                    stringBuilder.AppendLine($"MobileParty {audit} not found in {nameof(IObjectManager)}");
                    errorCount++;
                }
                else
                {
                    errorCount += CompareObjects(stringBuilder, audit, obj);
                }
            }

            stringBuilder.AppendLine($"Found {errorCount} errors in {dataToAudit.Count()} objects");

            return stringBuilder.ToString();
        }
        /// <summary>
        /// Compares fields and properties
        /// </summary>
        /// <returns>Returns count of errors</returns>
        public virtual int CompareObjects(StringBuilder stringBuilder, AuditData data, AuditingType obj)
        {
            return 0;
        }
        public abstract Response CreateResponseInstance(IEnumerable<AuditData> par1, string par2);
        public abstract Request CreateRequestInstance(IEnumerable<AuditData> par1);
    }
}
