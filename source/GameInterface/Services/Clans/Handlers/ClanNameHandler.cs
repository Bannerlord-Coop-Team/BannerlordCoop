using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using System;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Clans.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class ClanNameHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClanNameHandler>();

        public ClanNameHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ClanNameChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeClanName>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeClanName>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChanged> obj)
        {
            var payload = obj.What;
            network.SendAll(new NetworkChangeClanName(payload.ClanId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkChangeClanName> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var clan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            // Applying the name runs vanilla game code and the refresh touches the clan screen
            // UI; both must run on the main thread, not the network thread that delivered the
            // message. The server relays to the other clients only after it has applied the
            // change itself.
            GameLoopRunner.RunOnMainThread(() =>
            {
                if (Campaign.Current == null) return;

                try
                {
                    ClanNameChangePatch.RunOriginalChangeClanName(clan, new TextObject(payload.Name), new TextObject(payload.InformalName));

                    if (ModInformation.IsServer)
                    {
                        network.SendAll(new NetworkChangeClanName(payload.ClanId, payload.Name, payload.InformalName));
                    }

                    if (ScreenManager.TopScreen is GauntletClanScreen clanScreen)
                    {
                        clanScreen._dataSource?.RefreshValues();
                    }

                    InformationManager.DisplayMessage(new InformationMessage($"Clan {payload.ClanId} changed name to {payload.Name}"));
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply clan name change for clan ({clanId})", payload.ClanId);
                }
            });
        }
    }
}
