using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handles taking prisoners in GameInterface
    /// </summary>
    public class TakePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<TakePrisonerHandler>();

        public TakePrisonerHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<TakePrisoner>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TakePrisoner>(Handle);
        }

        private void Handle(MessagePayload<TakePrisoner> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<MobileParty>(payload.PartyId, out var party) == false)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.PartyId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.CharacterId, out var hero) == false)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.CharacterId);
                return;
            }

            TakePrisonerPatch.RunOriginalApplyInternal(party.Party, hero, payload.IsEventCalled);
        }
    }
}