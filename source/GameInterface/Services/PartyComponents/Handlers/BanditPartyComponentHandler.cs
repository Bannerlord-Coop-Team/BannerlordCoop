using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.PartyComponents.Patches.BanditPartyComponents;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Handlers
{
    internal class BanditPartyComponentHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly PartyComponentRegistry registry;

        private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentPatches>();

        public static FieldInfo Component_RelatedSettlement => typeof(BanditPartyComponent).GetField("_relatedSettlement", BindingFlags.NonPublic | BindingFlags.Instance);

        public BanditPartyComponentHandler(IMessageBroker messageBroker, INetwork network, PartyComponentRegistry registry, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.registry = registry;
            this.objectManager = objectManager;

            messageBroker.Subscribe<BanditPartyComponentUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateBanditPartyComponent>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BanditPartyComponentUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateBanditPartyComponent>(Handle);
        }

        private void Handle(MessagePayload<BanditPartyComponentUpdated> payload)
        {
            var obj = payload.What;

            if(registry.TryGetId(obj.Component, out string componentId) == false)
            {
                Logger.Error("Could not find {component} in registry \n"
                    + "Callstack: {callstack}", obj.Component.Name, Environment.StackTrace);
                return;
            }

            NetworkUpdateBanditPartyComponent message = new(componentId, (int)obj.ComponentType, obj.NewValue);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkUpdateBanditPartyComponent> payload)
        {
            var obj = payload.What;

            if (registry.TryGetValue(obj.ComponentId, out BanditPartyComponent component) == false)
            {
                Logger.Error("Could not find {component} in registry \n"
                    + "Callstack: {callstack}", obj.ComponentId, Environment.StackTrace);
                return;
            }

            using (new AllowedThread())
            {
                switch ((BanditPartyComponentType)obj.BanditPartyComponentType)
                {
                    case BanditPartyComponentType.Hideout:
                        objectManager.TryGetObject(obj.Value, out Settlement settlement);
                        component.Hideout = settlement.Hideout;
                        break;

                    case BanditPartyComponentType.IsBossParty:
                        component.IsBossParty = bool.Parse(obj.Value);
                        break;
                }
            }
        }
    }
}
