#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.Locations.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Locations.Handlers;

internal class SettlementOverlayFixtureHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementOverlayFixtureHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ISettlementInterface settlementInterface;

    private Hero fixtureHero;
    private CharacterObject fixtureCharacter;
    private Location fixtureLocation;
    private LocationCharacter fixtureEntry;
    private Settlement originalHeroSettlement;
    private CampaignVec2 originalPartyPosition;
    private string phase = "Idle";
    private string lastError;

    public SettlementOverlayFixtureHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;

        messageBroker.Subscribe<NetworkSettlementOverlayFixture>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSettlementOverlayFixture>(Handle);
    }

    public string DescribeState()
    {
        var mainParty = MobileParty.MainParty;
        var menuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "null";
        var settlementId = mainParty?.CurrentSettlement?.StringId ?? "null";
        var characterId = fixtureCharacter?.StringId ?? "null";
        var characterMissing = fixtureHero != null && fixtureHero.CharacterObject == null;

        return $"Phase={phase}; CharacterId={characterId}; HeroCharacterMissing={characterMissing}; " +
            $"SettlementId={settlementId}; MenuId={menuId}; Error={lastError ?? "none"}";
    }

    private void Handle(MessagePayload<NetworkSettlementOverlayFixture> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() => Apply(data), context: nameof(NetworkSettlementOverlayFixture));
    }

    private void Apply(NetworkSettlementOverlayFixture data)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var targetHero) ||
            targetHero != Hero.MainHero)
        {
            return;
        }

        try
        {
            lastError = null;
            switch (data.Operation)
            {
                case SettlementOverlayFixtureOperation.Inject:
                    Inject(data.SettlementId);
                    break;
                case SettlementOverlayFixtureOperation.Restore:
                    Restore();
                    break;
                case SettlementOverlayFixtureOperation.Cleanup:
                    Cleanup();
                    break;
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            Logger.Error(ex, "Settlement overlay fixture {Operation} failed", data.Operation);
        }
    }

    private void Inject(string settlementId)
    {
        if (fixtureHero != null)
        {
            throw new InvalidOperationException("Settlement overlay fixture is already active");
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement)) return;
        if (!settlement.IsVillage)
        {
            throw new InvalidOperationException($"Settlement {settlementId} is not a village");
        }

        var mainParty = MobileParty.MainParty;
        if (mainParty == null || mainParty.CurrentSettlement != null || PlayerEncounter.Current != null)
        {
            throw new InvalidOperationException("The client must start on the campaign map outside a settlement");
        }

        var hero = Hero.AllAliveHeroes
            .Where(hero => hero != Hero.MainHero &&
                hero.CharacterObject != null &&
                hero.PartyBelongedTo == null &&
                hero.PartyBelongedToAsPrisoner == null)
            .OrderBy(hero => hero.StringId)
            .FirstOrDefault();
        if (hero == null)
        {
            throw new InvalidOperationException("No partyless fixture hero was available");
        }

        var location = settlement.LocationComplex?.GetLocationWithId("village_center");
        if (location == null)
        {
            throw new InvalidOperationException($"Village {settlementId} has no village_center location");
        }

        var character = hero.CharacterObject;
        var entry = LocationCharacterFactory.Create(
            character,
            originParty: null,
            specialItem: null,
            spawnTag: "sp_notable",
            actionSetCode: null,
            behaviorsMethodName: null,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: true,
            useCivilianEquipment: true);

        fixtureHero = hero;
        fixtureCharacter = character;
        fixtureLocation = location;
        fixtureEntry = entry;
        originalHeroSettlement = hero.StayingInSettlement;
        originalPartyPosition = mainParty.Anchor.Position;

        try
        {
            using (new AllowedThread())
            {
                hero.StayingInSettlement = settlement;
                LocationCharacterListPatches.AddEntry(location, entry);
                hero._characterObject = null;
                settlementInterface.PartyEnterSettlement(mainParty, settlement);
                settlementInterface.StartSettlementEncounter(mainParty, settlement);
            }

            mainParty.SetMoveModeHold();
            phase = "Injected";
            GameMenu.SwitchToMenu("village");
        }
        catch
        {
            try
            {
                Cleanup();
            }
            catch (Exception cleanupException)
            {
                Logger.Error(cleanupException, "Settlement overlay fixture rollback failed");
            }

            throw;
        }
    }

    private void Restore()
    {
        if (fixtureHero == null || fixtureCharacter == null)
        {
            throw new InvalidOperationException("Settlement overlay fixture is not active");
        }

        fixtureHero._characterObject = fixtureCharacter;
        phase = "Restored";
        GameMenu.SwitchToMenu("village");
    }

    private void Cleanup()
    {
        if (fixtureHero == null)
        {
            throw new InvalidOperationException("Settlement overlay fixture is not active");
        }

        var mainParty = MobileParty.MainParty;
        using (new AllowedThread())
        {
            fixtureHero._characterObject = fixtureCharacter;
            if (fixtureEntry != null &&
                fixtureLocation?.GetCharacterList()?.Contains(fixtureEntry) == true)
            {
                LocationCharacterListPatches.RemoveEntry(fixtureLocation, fixtureEntry);
            }
            fixtureHero.StayingInSettlement = originalHeroSettlement;
            if (PlayerEncounter.Current != null || mainParty.CurrentSettlement != null)
            {
                settlementInterface.EndSettlementEncounter();
            }
            mainParty.Anchor.SetPosition(originalPartyPosition);
        }

        fixtureHero = null;
        fixtureCharacter = null;
        fixtureLocation = null;
        fixtureEntry = null;
        originalHeroSettlement = null;
        phase = "Cleaned";
    }
}
#endif
