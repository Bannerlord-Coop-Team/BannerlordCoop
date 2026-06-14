using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Stances.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
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
        private static readonly ILogger Logger = LogManager.GetLogger<FactionStanceHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public FactionStanceHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Subscribe<MakePeaceChanged>(HandleMakePeace);
        }

        private void HandleDeclareWar(MessagePayload<DeclareWarChanged> obj)
        {
            var payload = obj.What;
            if (!TryGetFaction(payload.Faction1Id, out var faction1)) return;
            if (!TryGetFaction(payload.Faction2Id, out var faction2)) return;

            // ApplyInternal is the funnel for every war cause; calling it directly (publicized)
            // preserves the original DeclareWarDetail so detail-sensitive client listeners match the server.
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    DeclareWarAction.ApplyInternal(faction1, faction2, (DeclareWarAction.DeclareWarDetail)payload.Detail);
                }
            }, true);
        }

        private void HandleMakePeace(MessagePayload<MakePeaceChanged> obj)
        {
            var payload = obj.What;
            if (!TryGetFaction(payload.Faction1Id, out var faction1)) return;
            if (!TryGetFaction(payload.Faction2Id, out var faction2)) return;

            // ApplyInternal is the funnel for every peace cause; calling it directly (publicized)
            // preserves the original MakePeaceDetail and the daily tribute.
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    MakePeaceAction.ApplyInternal(faction1, faction2, payload.DailyTribute, payload.DailyTributeDuration, (MakePeaceAction.MakePeaceDetail)payload.Detail);
                }
            }, true);
        }

        private bool TryGetFaction(string id, out IFaction faction)
        {
            if (objectManager.TryGetObject(id, out Kingdom kingdom))
            {
                faction = kingdom;
                return true;
            }
            if (objectManager.TryGetObject(id, out Clan clan))
            {
                faction = clan;
                return true;
            }
            Logger.Verbose("Faction not found in FactionStanceHandler with id: {id}", id);
            faction = null;
            return false;
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<DeclareWarChanged>(HandleDeclareWar);
            messageBroker.Unsubscribe<MakePeaceChanged>(HandleMakePeace);
        }
    }
}
