using Common.Messaging;
using LiteNetLib;
using Missions.Services.Network.Messages;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Handlers
{
    /// <summary>
    /// Handles the disconnection of server popup messages
    /// </summary>
    public interface IServerDisconnectHandler : IHandler, IDisposable { }

    /// <inheritdoc cref="IServerDisconnectHandler"/>
    internal class ServerDisconnectHandler : IServerDisconnectHandler
    {
        private IMessageBroker _messageBroker;

        public ServerDisconnectHandler(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;

            _messageBroker.Subscribe<ServerDisconnected>(Handle_ServerDisconnected);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<ServerDisconnected>(Handle_ServerDisconnected);
        }

        private void Handle_ServerDisconnected(MessagePayload<ServerDisconnected> obj)
        {
            var payload = obj.What;

            var reason = payload.DisconnectInfo.Reason;

            switch (reason)
            {
                case DisconnectReason.HostUnreachable:
                case DisconnectReason.ConnectionFailed:
                    InformationManager.ShowInquiry(CreateUnreachableInquiry(), true);
                    break;
                case DisconnectReason.ConnectionRejected:
                    InformationManager.ShowInquiry(CreateCouldNotConnectInquiry(), true);
                    break;
                default:
                    InformationManager.ShowInquiry(CreateDisconnectedInquiry(), true);
                    break;
            }
        }

        private InquiryData CreateUnreachableInquiry()
        {
            return new InquiryData(
                "Server is unreachable",
                "The server could not be reached.",
                true,
                false,
                "Close",
                "",
                null,
                null);
        }

        private InquiryData CreateDisconnectedInquiry()
        {
            return new InquiryData(
                "Disconnected From Server",
                "You have been disconnected from the server, returning to the main menu may take a few seconds.",
                true,
                false,
                "Back to Menu",
                "",
                new Action(() => { MBGameManager.EndGame(); }),
                null);
        }

        private InquiryData CreateCouldNotConnectInquiry()
        {
            return new InquiryData(
                "Unable to Connect to Server",
                "Server rejected connection, possible mod version mismatch.",
                true,
                false,
                "Close",
                "",
                null,
                null);
        }
    }
}
