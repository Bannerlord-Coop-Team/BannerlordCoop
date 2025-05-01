using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers
{
    class TroopRosterLifetimeHandler : IHandler
    {
        private readonly INetwork network;
        private readonly IObjectManager objectManager;

        public TroopRosterLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            messageBroker.Subscribe<TroopRosterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateTroopRoster>(Handle);
            this.network = network;
            this.objectManager = objectManager;
        }

        private void Handle(MessagePayload<NetworkCreateTroopRoster> payload)
        {
            var newObj = ObjectHelper.SkipConstructor<TroopRoster>();

            objectManager.AddExisting(payload.What.Id, newObj);
        }

        private void Handle(MessagePayload<TroopRosterCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.Instance, out var newId))
            {
                network.SendAll(new NetworkCreateTroopRoster(newId));
            }
        }

        public void Dispose()
        {
        }
    }

    public class TroopRosterCreated : IEvent
    {
        public TroopRoster Instance { get; }

        public TroopRosterCreated(TroopRoster instance)
        {
            Instance = instance;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class NetworkCreateTroopRoster : ICommand
    {
        [ProtoMember(1)]
        public string Id { get; }

        public NetworkCreateTroopRoster(string id)
        {
            Id = id;
        }
    }
}


