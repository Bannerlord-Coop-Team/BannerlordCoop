#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Locations;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Entity;
using Newtonsoft.Json;
using SandBox.GauntletUI.Menu;
using SandBox.View.Map;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Locations.Commands;

public sealed class Issue1837NotableProbeCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public Issue1837NotableProbeCoopCommand(IObjectManager objectManager, IPlayerManager playerManager, IControllerIdProvider controllerIdProvider)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
    }

    public string Prefix => "coop.debug.siege";
    public string Name => "issue1837_notable_state";
    public string Description => "Reads settlement notables, location rosters and the current rendered town overlay.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("settlementId", "The registered target settlement id."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (Campaign.Current == null || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return new CoopCommandResult(false, "The campaign or settlement is unavailable.", "command_failed");

        var overlay = ModInformation.IsClient
            ? MapScreen.Instance?._menuViewContext?.GetMenuView<GauntletMenuOverlayBaseView>()?._overlayDataSource as SettlementMenuOverlayVM
            : null;
        object trackerState = null;
        if (ModInformation.IsServer && ContainerProvider.TryResolve<SettlementPopulationTracker>(out var tracker))
        {
            // These private mod fields are not publicized; the diagnostic only reads them on the game thread.
            var populated = typeof(SettlementPopulationTracker)
                .GetField("populatedSettlements", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tracker) as IDictionary;
            var visitors = typeof(SettlementPopulationTracker)
                .GetField("playerPartySettlements", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tracker) as IDictionary;
            if (populated == null || visitors == null || visitors.Count > 32)
                return new CoopCommandResult(false, "Tracker diagnostic fields unavailable or visitor bound exceeded.", "command_failed");
            trackerState = new
            {
                populated = populated.Contains(settlement.StringId),
                visitors = visitors.Keys.Cast<string>().Select(key => new
                {
                    partyId = key,
                    settlementId = (string)visitors[key],
                }).ToArray(),
            };
        }

        var locationList = settlement.LocationComplex?.GetListOfLocations()?.ToArray();
        if (locationList == null || locationList.Length > 32 || locationList.Any(location => location == null ||
            location.GetCharacterList() == null || location.GetCharacterList().Count() > 512 ||
            location.GetCharacterList().Any(character => character?.Character == null)) ||
            settlement.Notables == null || settlement.Notables.Count > 64 || settlement.Notables.Any(hero => hero?.CharacterObject == null) ||
            playerManager.Players.Count() > 32)
            return new CoopCommandResult(false, "Notable/location diagnostic unavailable or observation bound exceeded.", "command_failed");
        var locations = locationList.Select(location => new
        {
            locationId = location.StringId,
            networkId = Id(location),
            characters = location.GetCharacterList().Select(character => new
            {
                characterId = character.Character.StringId,
                networkId = Id(character.Character),
                heroId = character.Character.HeroObject?.StringId,
                listedInOverlay = Campaign.Current.Models.HeroAgentLocationModel.WillBeListedInOverlay(character),
            }).ToArray(),
        }).ToArray();
        var result = new
        {
            side = ModInformation.IsServer ? "server" : "client",
            observedUtc = DateTime.UtcNow.ToString("O"),
            localControllerId = controllerIdProvider.ControllerId,
            settlementId = settlement.StringId,
            settlementName = settlement.Name?.ToString(),
            factionId = Id(settlement.MapFaction),
            ownerClanId = Id(settlement.OwnerClan),
            siegeActive = settlement.SiegeEvent != null,
            isTown = settlement.IsTown,
            networkId = Id(settlement),
            notables = settlement.Notables.Select(hero => new
            {
                heroId = hero.StringId,
                heroName = hero.Name?.ToString(),
                heroNetworkId = Id(hero),
                characterId = hero.CharacterObject.StringId,
                characterNetworkId = Id(hero.CharacterObject),
                isAlive = !hero.IsDead,
                state = hero._heroState.ToString(),
                currentSettlementId = hero.CurrentSettlement?.StringId,
                locationId = settlement.LocationComplex?.GetLocationOfCharacter(hero)?.StringId,
            }).ToArray(),
            parties = playerManager.Players.Select(player =>
            {
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
                return new
                {
                    controllerId = player.ControllerId,
                    connected = playerManager.IsConnected(player),
                    partyId = party?.StringId,
                    partyNetworkId = Id(party),
                    currentSettlementId = party?.CurrentSettlement?.StringId,
                    mapEventId = Id(party?.MapEvent),
                    siegeCampId = Id(party?.BesiegerCamp),
                    armyId = Id(party?.Army),
                    factionId = Id(party?.MapFaction),
                    atWarWithTarget = party?.MapFaction?.IsAtWarWith(settlement.MapFaction),
                    gateDistanceSquared = party == null ? (float?)null : party.Position.ToVec2().DistanceSquared(settlement.GatePosition.ToVec2()),
                    eventState = party?.MapEvent?.BattleState.ToString(),
                    isSiegeAssault = party?.MapEvent?.IsSiegeAssault,
                    eventWinner = party?.MapEvent?.WinningSide.ToString(),
                    eventHasWinner = party?.MapEvent?.HasWinner,
                };
            }).ToArray(),
            tracker = trackerState,
            locations,
            menuId = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId,
            encounterSettlementId = ModInformation.IsClient ? PlayerEncounter.EncounterSettlement?.StringId : null,
            overlaySettlementId = overlay?._settlement?.StringId,
            overlayType = overlay?._type.ToString(),
            overlayCharacters = overlay?.CharacterList.Select(item => new
            {
                characterId = item.Character?.StringId,
                networkId = Id(item.Character),
                heroId = item.Character?.HeroObject?.StringId,
            }).ToArray(),
            menuLocationIds = ModInformation.IsClient
                ? Campaign.Current.GameMenuManager.MenuLocations.Where(location => location != null).Select(location => location.StringId).ToArray()
                : Array.Empty<string>(),
        };
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(result));
    }

    private string Id(object value)
    {
        return value != null && objectManager.TryGetId(value, out var id) ? id : null;
    }
}
#endif
