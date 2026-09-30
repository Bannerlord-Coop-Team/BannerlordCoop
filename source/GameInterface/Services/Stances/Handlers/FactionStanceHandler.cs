using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Interfaces;
using GameInterface.Services.Stances.Messages;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Stances.Handlers
{
    /// <summary>
    /// Applies replicated faction stance changes (war / peace) on the receiving machine by
    /// re-running the vanilla action under AllowedThread, which re-fires the campaign events
    /// client-side without re-announcing.
    /// </summary>
    public class FactionStanceHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IFactionInterface factionInterface;

        public FactionStanceHandler(IMessageBroker messageBroker, IFactionInterface factionInterface)
        {
            this.messageBroker = messageBroker;
            this.factionInterface = factionInterface;
            messageBroker.Subscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Subscribe<MakePeaceChanged>(HandleMakePeace);
        }

        private void HandleDeclareWar(MessagePayload<DeclareWarChanged> obj)
        {
            var payload = obj.What;

            // ApplyInternal is the funnel for every war cause; calling it directly (publicized)
            // preserves the original DeclareWarDetail so detail-sensitive client listeners match the server.
            GameThread.RunSafe(() =>
            {
                if (!factionInterface.TryGetFaction(payload.Faction1Id, out var faction1)) return;
                if (!factionInterface.TryGetFaction(payload.Faction2Id, out var faction2)) return;

                using (new AllowedThread())
                {
                    DeclareWarAction.ApplyInternal(faction1, faction2, (DeclareWarAction.DeclareWarDetail)payload.Detail);
                }
            });
        }

        private void HandleMakePeace(MessagePayload<MakePeaceChanged> obj)
        {
            var payload = obj.What;

            // ApplyInternal is the funnel for every peace cause; calling it directly (publicized)
            // preserves the original MakePeaceDetail and the daily tribute.
            GameThread.RunSafe(() =>
            {
                if (!factionInterface.TryGetFaction(payload.Faction1Id, out var faction1)) return;
                if (!factionInterface.TryGetFaction(payload.Faction2Id, out var faction2)) return;

                using (new AllowedThread())
                {
                    MakePeaceAction.ApplyInternal(faction1, faction2, payload.DailyTribute, payload.DailyTributeDuration, (MakePeaceAction.MakePeaceDetail)payload.Detail);
                }
            });
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Unsubscribe<MakePeaceChanged>(HandleMakePeace);
        }
    }
}
