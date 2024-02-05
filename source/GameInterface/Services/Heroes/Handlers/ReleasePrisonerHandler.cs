using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handles releasing prisoners in GameInterface
    /// </summary>
    public class ReleasePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ReleasePrisonerHandler>();

        public ReleasePrisonerHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ReleasePrisoner>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ReleasePrisoner>(Handle);
        }

        private void Handle(MessagePayload<ReleasePrisoner> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Hero>(payload.HeroId, out var hero) == false)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.HeroId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.FacilitatorId, out var facilitator) == false && payload.FacilitatorId != null)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.HeroId);
                return;
            }

            ReleasePrisonerPatch.RunOriginalApplyInternal(hero, (EndCaptivityDetail)payload.EndCaptivityDetail, facilitator);
        }
    }
}