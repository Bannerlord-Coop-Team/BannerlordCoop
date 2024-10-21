using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Handlers
{
    public class PartyVisualLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;


        public PartyVisualLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<PartyVisualCreated>(Handle);
            messageBroker.Subscribe<NetworkCreatePartyVisual>(Handle);
            messageBroker.Subscribe<PartyVisualDestroyed>(Handle);
            messageBroker.Subscribe<NetworkDestroyPartyVisual>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyVisualCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreatePartyVisual>(Handle);
            messageBroker.Unsubscribe<PartyVisualDestroyed>(Handle);
            messageBroker.Unsubscribe<NetworkDestroyPartyVisual>(Handle);
        }


        private void Handle(MessagePayload<PartyVisualCreated> payload)
        {
            objectManager.AddNewObject(payload.What.PartyVisual, out var visualId);
            objectManager.TryGetId(payload.What.PartyBase, out string partyBaseId);

            network.SendAll(new NetworkCreatePartyVisual(visualId, partyBaseId));
        }

        private void Handle(MessagePayload<NetworkCreatePartyVisual> payload)
        {
            objectManager.TryGetObject<PartyBase>(payload.What.PartyBaseId, out var partyBase);

            PartyVisual newVisual = new PartyVisual(partyBase);

            objectManager.AddExisting(payload.What.PartyVisualId, newVisual);
        }

        private void Handle(MessagePayload<PartyVisualDestroyed> payload)
        {
            objectManager.TryGetId(payload.What.PartyVisual, out string visualId);
            objectManager.Remove(payload.What.PartyVisual);

            network.SendAll(new NetworkDestroyPartyVisual(visualId));
        }

        private void Handle(MessagePayload<NetworkDestroyPartyVisual> payload)
        {
            objectManager.TryGetObject(payload.What.PartyVisualId, out PartyVisual partyVisual);
            objectManager.Remove(partyVisual);
        }
    }
}
