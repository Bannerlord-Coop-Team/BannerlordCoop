using Common;
using Common.Messaging;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// On a joining client, restores the player's alley management data (garrison + overseer) from the
/// transferred CoopSession once that client's main hero is known, so its manage-alley menus work
/// after joining. Mirrors WorkshopsCampaignBehaviorInitializationHandler.
/// </summary>
internal class AlleyInitializationHandler : IHandler
{
    // Alley.State is a private-setter, non-saved, MainHero-relative enum. The host saves it as
    // OccupiedByGangLeader (host has no main hero) and vanilla's load re-derive runs before this client's
    // main hero exists, so owned alleys load gang-occupied. We re-set it for the owning client on load.
    private static readonly MethodInfo StateSetter = AccessTools.PropertySetter(typeof(Alley), nameof(Alley.State));

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IAlleyCampaignBehaviorInterface behaviorInterface;

    private AlleyPlayerData alleyPlayerData;

    public AlleyInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IAlleyCampaignBehaviorInterface behaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorInterface = behaviorInterface;

        messageBroker.Subscribe<InitializeClientAlleyData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientAlleyData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientAlleyData> payload)
    {
        alleyPlayerData = payload.What.AlleyPlayerData;
    }

    private void Handle(MessagePayload<PlayerHeroChanged> payload)
    {
        if (ModInformation.IsServer) return;

        var newHero = payload.What.NewHero;
        if (newHero == null) return;

        var managementData = alleyPlayerData?.ManagementDataPerAlley;

        GameThread.RunSafe(() =>
        {
            // Re-derive State for every alley this client owns (owner is saved, so OwnedAlleys is correct),
            // independent of whether we have management data for it.
            foreach (var alley in newHero.OwnedAlleys)
            {
                StateSetter?.Invoke(alley, new object[] { Alley.AreaState.OccupiedByPlayer });
            }

            if (managementData == null) return;

            foreach (var pair in managementData)
            {
                if (!objectManager.TryGetObjectWithLogging<Alley>(pair.Key, out var alley)) continue;
                if (alley.Owner != newHero) continue;

                Hero overseer = null;
                if (pair.Value.OverseerId != null) objectManager.TryGetObjectWithLogging(pair.Value.OverseerId, out overseer);

                behaviorInterface.AddOrUpdatePlayerAlleyData(alley, overseer, AlleyGarrisonData.FromData(pair.Value.Garrison, objectManager));
            }
        });
    }
}
