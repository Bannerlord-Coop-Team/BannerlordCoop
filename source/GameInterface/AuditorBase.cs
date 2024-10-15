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
    public interface IAuditResponse<AuditDataType> : IEvent
    {
        public string ServerAuditResult { get; }
        public AuditDataType[] Data { get; }
    }
    public interface IAuditRequest<AuditDataType> : ICommand
    {
        public AuditDataType[] Data { get; }
    }
    public interface IAuditInfo
    {
        public string Name { get; }
        public string StringId { get; }
    }

    /// <summary>
    /// A base class that contains default audit behavior
    /// </summary>
    /// <typeparam name="RequestType"><see cref="IAuditRequest"/> type</typeparam>
    /// <typeparam name="ResponseType"><see cref="IAuditRequest"/> type</typeparam>
    /// <typeparam name="AuditDataType">Data type used to store data of a single object</typeparam>
    public abstract class AuditorBase<RequestType, ResponseType, AuditDataType> : IAuditor
            where RequestType : IAuditRequest<AuditDataType>
            where ResponseType : IAuditResponse<AuditDataType>
            where AuditDataType : class
    {
        protected static readonly ILogger Logger = LogManager.GetLogger<AuditorBase<RequestType, ResponseType, AuditDataType>>();
        protected readonly IMessageBroker messageBroker;
        protected readonly INetwork network;
        protected readonly IObjectManager objectManager;
        protected readonly INetworkConfiguration configuration;
        private TaskCompletionSource<string> tcs;

        public AuditorBase(
            IMessageBroker messageBroker,
            INetwork network,
            IObjectManager objectManager,
            INetworkConfiguration configuration)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            this.configuration = configuration;
            messageBroker.Subscribe<RequestType>(Handle_Request);
            messageBroker.Subscribe<ResponseType>(Handle_Response);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<RequestType>(Handle_Request);
            messageBroker.Unsubscribe<ResponseType>(Handle_Response);
        }

        protected virtual void Handle_Response(MessagePayload<ResponseType> payload)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Server Audit Result:");
            stringBuilder.AppendLine(payload.What.ServerAuditResult);

            stringBuilder.AppendLine("Client Audit Result:");
            stringBuilder.AppendLine(AuditData(payload.What.Data));

            tcs.SetResult(stringBuilder.ToString());
        }

        protected virtual void Handle_Request(MessagePayload<RequestType> payload)
        {
            var response = CreateResponse(payload.What);
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

            var request = CreateRequest();

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
        protected abstract string AuditData(IEnumerable<AuditDataType> data);
        protected abstract ResponseType CreateResponse(RequestType request);
        protected abstract RequestType CreateRequest();
    }
    /// <summary>
    /// A base class that contains default audit behavior and <see cref="AuditorBase{RequestType, ResponseType, AuditDataType, AuditingType}.AuditData(IEnumerable{AuditDataType})"/> implementation
    /// </summary>
    /// <typeparam name="RequestType"><see cref="IAuditRequest"/> type</typeparam>
    /// <typeparam name="ResponseType"><see cref="IAuditRequest"/> type</typeparam>
    /// <typeparam name="AuditDataType">Data type used to store data of a single object</typeparam>
    /// <typeparam name="AuditingType">Type, being audited (like a <seealso cref="MobileParty"/>, <seealso cref="TaleWorlds.CampaignSystem.Army"/> and etc.)</typeparam>
    public abstract class AuditorBase<RequestType, ResponseType, AuditDataType, AuditingType> : AuditorBase<RequestType, ResponseType, AuditDataType>
            where RequestType : IAuditRequest<AuditDataType>
            where ResponseType : IAuditResponse<AuditDataType>
            where AuditDataType : class, IAuditInfo
            where AuditingType : class
    {
        public AuditorBase(IMessageBroker messageBroker,
            INetwork network,
            IObjectManager objectManager,
            INetworkConfiguration configuration) : base(messageBroker, network, objectManager, configuration) { }
        protected override string AuditData(IEnumerable<AuditDataType> data)
        {
            var typeName = typeof(AuditingType).Name;
            var stringBuilder = new StringBuilder();

            var errorCount = 0;
            var objectsCount = Objects.Count();
            stringBuilder.AppendLine($"Auditing {objectsCount} objects");

            if (objectsCount != data.Count())
            {
                Logger.Error($"{typeName} count mismatch: {objectsCount} != {data.Count()}");
                stringBuilder.AppendLine($"{typeName} count mismatch: {objectsCount} != {data.Count()}");
            }

            foreach (var audit in data)
            {
                if (objectManager.TryGetObject<AuditingType>(audit.StringId, out var obj) == false)
                {
                    Logger.Error($"{typeName} {audit.Name} not found in {objectManager.GetType().Name}");
                    stringBuilder.AppendLine($"{typeName} {audit} not found in {nameof(IObjectManager)}");
                    errorCount++;
                }
            }

            stringBuilder.AppendLine($"Found {errorCount} errors in {data.Count()} objects");

            return stringBuilder.ToString();
        }
        protected override RequestType CreateRequest() => (RequestType)Activator.CreateInstance(typeof(RequestType), new object[] { GetAuditData() });
        protected override ResponseType CreateResponse(RequestType request) => (ResponseType)Activator.CreateInstance(typeof(ResponseType), new object[] { GetAuditData(), AuditData(request.Data) });
        public abstract IEnumerable<AuditDataType> GetAuditData();
        public abstract IEnumerable<AuditingType> Objects { get; }
    }
}
