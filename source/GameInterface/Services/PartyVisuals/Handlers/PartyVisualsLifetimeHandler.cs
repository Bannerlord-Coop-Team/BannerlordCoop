using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyVisuals.Messages;
using Serilog;
using System;
using static TaleWorlds.CampaignSystem.Army;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Common.Util;
using SandBox.View.Map;
using System.Reflection;
using GameInterface.Services.PartyBases.Extensions;

namespace GameInterface.Services.PartyVisuals.Handlers
{
    public class PartyVisualsLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PartyVisualsLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public PartyVisualsLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<PartyVisualCreated>(Handle_VisualCreated);
            messageBroker.Subscribe<NetworkCreatePartyVisual>(Handle_CreateVisual);

            messageBroker.Subscribe<PartyVisualDestroyed>(Handle_VisualDestroyed);
            messageBroker.Subscribe<NetworkDestroyPartyVisual>(Handle_DestroyVisual);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void Handle_VisualCreated(MessagePayload<PartyVisualCreated> payload)
        {
            var partyVisual = payload.What.PartyVisual;
            var partyBase = payload.What.Party;

            if (objectManager.TryGetId(partyBase.MobileParty, out var mobilePartyId) == false) return;

            var message = new NetworkCreatePartyVisual(mobilePartyId);
            network.SendAll(message);
        }

        private void Handle_CreateVisual(MessagePayload<NetworkCreatePartyVisual> payload)
        {
            var mobilePartyId = payload.What.MobilePartyId;

            if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    typeof(PartyVisualManager).GetMethod("AddNewPartyVisualForParty", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(PartyVisualManager.Current, new object[] { mobileParty } ); 
                }
            });
        }

        private void Handle_VisualDestroyed(MessagePayload<PartyVisualDestroyed> payload)
        {
            var mobileParty = payload.What.PartyVisual.PartyBase.MobileParty;
            if (objectManager.TryGetId(mobileParty, out string mobilePartyId) == false)
            {
                Logger.Error("Could not find {mobileParty} in {name}", mobileParty.Name, nameof(IObjectManager));
                return;
            }

            var message = new NetworkDestroyPartyVisual(mobilePartyId);
            network.SendAll(message);
        }

        private void Handle_DestroyVisual(MessagePayload<NetworkDestroyPartyVisual> payload)
        {
            var stringId = payload.What.MobilePartyId;

            if (objectManager.TryGetObject(stringId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Failed to find mobile party with stringId {stringId}", stringId);
                return;
            }

            PartyVisualsLifetimePatches.OverrideDestroyPartyVisual(mobileParty.Party.GetPartyVisual());
        }
    }
}