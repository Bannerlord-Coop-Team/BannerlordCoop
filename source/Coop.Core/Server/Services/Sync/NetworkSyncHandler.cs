﻿using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Sync;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using LiteNetLib;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Sync
{
    public class NetworkSyncHandler : IHandler
    {

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        private readonly HashSet<NetPeer> waiting = new();

        public NetworkSyncHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkSyncWait>(Handle);
            messageBroker.Subscribe<NetworkSyncComplete>(Handle);
        }

        private void Handle(MessagePayload<NetworkSyncWait> p)
        {
            waiting.Add(p.Who as NetPeer);
            messageBroker.Publish(this, new SendInformationMessage(
                    string.Format("Game paused, client {0} out of sync",
                        (p.Who as NetPeer).EndPoint.Address.ToString())
                    ));

            network.SendAll(new NetworkTimeSpeedChanged(TimeControlEnum.Pause));
        }

        private void Handle(MessagePayload<NetworkSyncComplete> p)
        {
            waiting.Remove(p.Who as NetPeer);
            messageBroker.Publish(this, new SendInformationMessage(
                    string.Format("Client {0} synchronised", 
                        (p.Who as NetPeer).EndPoint.Address.ToString())
                    ));

            if (waiting.Count == 0)
            {
                //TODO: maybe remember original timespeed
                network.SendAll(new NetworkTimeSpeedChanged(TimeControlEnum.Play_1x));
            }
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkSyncWait>(Handle);
            messageBroker.Unsubscribe<NetworkSyncComplete>(Handle);
        }
    }
}
