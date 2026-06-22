using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers
{
    /// <summary>
    /// Replicates server-authoritative clan renown changes to clients. Clan.Renown's setter is JIT-inlined into
    /// its writers, so it can't be AutoSynced through a setter prefix; the new absolute renown is published from
    /// Clan.AddRenown / ResetClanRenown on the server (ClanRenownPatch) and applied here on clients.
    /// </summary>
    public class ClanRenownHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClanRenownHandler>();

        public ClanRenownHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ClanRenownChanged>(Handle);
            messageBroker.Subscribe<NetworkClanRenownChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanRenownChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanRenownChanged>(Handle);
        }

        private void Handle(MessagePayload<ClanRenownChanged> obj)
        {
            var payload = obj.What;
            network.SendAll(new NetworkClanRenownChanged(payload.ClanId, payload.Renown));
        }

        private void Handle(MessagePayload<NetworkClanRenownChanged> obj)
        {
            var payload = obj.What;

            // Resolve and apply on the game thread. The object registry is mutated on the game thread, so a
            // lookup on the poll thread can race a registration and miss the clan; doing it inside the queued
            // action serializes it with that work. RunSafe logs a thrown apply instead of letting it kill the
            // game-thread pump.
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObject<Clan>(payload.ClanId, out var clan))
                {
                    Logger.Error("Unable to find clan ({clanId}) for renown change", payload.ClanId);
                    return;
                }

                // Apply the server's absolute value (not a delta) so clients converge. AllowedThread keeps any
                // patches on the write path standing down on the receive side.
                using (new AllowedThread())
                {
                    clan.Renown = payload.Renown;
                }
            }, context: $"apply clan renown for {payload.ClanId}");
        }
    }
}
