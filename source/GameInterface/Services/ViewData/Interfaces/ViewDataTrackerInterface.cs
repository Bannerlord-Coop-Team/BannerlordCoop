using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ViewData.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ViewData.Interfaces;

public interface IViewDataTrackerInterface : IGameAbstraction
{
    PlayerViewData CreatePlayerViewData();
    void LoadPlayerViewData(PlayerViewData viewData);
    void SetPlayerViewData(string playerHeroId, PlayerViewData viewData);
}

public class ViewDataTrackerInterface : IViewDataTrackerInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;

    private ViewDataPlayerData ViewDataPlayerData => coopSessionProvider.CoopSession.ViewDataPlayerData;

    public ViewDataTrackerInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public PlayerViewData CreatePlayerViewData()
    {
        var tracker = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();

        var examinedPrisonerCharacters = new Dictionary<string, int>();
        foreach (var entry in tracker._examinedPrisonerCharacterList)
        {
            if (objectManager.TryGetId(entry.Key, out var characterId))
                examinedPrisonerCharacters[characterId] = entry.Value;
        }

        var pinnedObjects = GetPinCounts().Keys;

        return new PlayerViewData
        {
            IsMapBarExtended = tracker._isMapBarExtended,
            PartySortType = tracker._partySortType,
            IsPartySortAscending = tracker._isPartySortAscending,
            PartyTroopLocks = tracker._partyTroopLocks.ToList(),
            PartyPrisonerLocks = tracker._partyPrisonerLocks.ToList(),
            QuestSortTypeSelection = tracker._questSortTypeSelection,
            IsCharacterNotificationActive = tracker._isCharacterNotificationActive,
            NumOfPerks = tracker._numOfPerks,
            BookmarkedHeroIds = GetIds(tracker._encyclopediaBookmarkedHeroes),
            BookmarkedShipHullIds = GetIds(tracker._encyclopediaBookmarkedShips),
            BookmarkedClanIds = GetIds(tracker._encyclopediaBookmarkedClans),
            // Concepts and quests aren't registered, so they're saved by StringId
            BookmarkedConceptIds = tracker._encyclopediaBookmarkedConcepts.Select(concept => concept.StringId).ToList(),
            BookmarkedKingdomIds = GetIds(tracker._encyclopediaBookmarkedKingdoms),
            BookmarkedSettlementIds = GetIds(tracker._encyclopediaBookmarkedSettlements),
            BookmarkedUnitIds = GetIds(tracker._encyclopediaBookmarkedUnits),
            QuestSelectionId = tracker._questSelection?.StringId,
            UnExaminedArmyIds = GetIds(tracker._unExaminedArmies),
            ExaminedPrisonerCharacters = examinedPrisonerCharacters,
            UnexaminedFigureheadIds = GetIds(tracker._unexaminedFigureheads),
            TrackedSettlementIds = GetIds(pinnedObjects.OfType<Settlement>()),
            TrackedPartyIds = GetIds(pinnedObjects.OfType<MobileParty>()),
        };
    }

    public void LoadPlayerViewData(PlayerViewData viewData)
    {
        var tracker = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();
        viewData ??= new PlayerViewData();

        // Replaces whatever the server save held, which isn't this player's view data
        tracker._isMapBarExtended = viewData.IsMapBarExtended;
        tracker._partySortType = viewData.PartySortType;
        tracker._isPartySortAscending = viewData.IsPartySortAscending;
        tracker._partyTroopLocks = viewData.PartyTroopLocks?.ToList() ?? new List<string>();
        tracker._partyPrisonerLocks = viewData.PartyPrisonerLocks?.ToList() ?? new List<string>();
        tracker._questSortTypeSelection = viewData.QuestSortTypeSelection;
        tracker._isCharacterNotificationActive = viewData.IsCharacterNotificationActive;
        tracker._numOfPerks = viewData.NumOfPerks;
        tracker._encyclopediaBookmarkedHeroes = GetObjects<Hero>(viewData.BookmarkedHeroIds);
        tracker._encyclopediaBookmarkedShips = GetObjects<ShipHull>(viewData.BookmarkedShipHullIds);
        tracker._encyclopediaBookmarkedClans = GetObjects<Clan>(viewData.BookmarkedClanIds);
        tracker._encyclopediaBookmarkedConcepts = (viewData.BookmarkedConceptIds ?? new List<string>())
            .Select(conceptId => MBObjectManager.Instance.GetObject<Concept>(conceptId))
            .Where(concept => concept != null)
            .ToList();
        tracker._encyclopediaBookmarkedKingdoms = GetObjects<Kingdom>(viewData.BookmarkedKingdomIds);
        tracker._encyclopediaBookmarkedSettlements = GetObjects<Settlement>(viewData.BookmarkedSettlementIds);
        tracker._encyclopediaBookmarkedUnits = GetObjects<CharacterObject>(viewData.BookmarkedUnitIds);
        tracker._questSelection = viewData.QuestSelectionId == null
            ? null
            : Campaign.Current.QuestManager.Quests.FirstOrDefault(quest => quest.StringId == viewData.QuestSelectionId);
        tracker._unExaminedArmies = GetObjects<Army>(viewData.UnExaminedArmyIds);
        tracker._examinedPrisonerCharacterList = new Dictionary<CharacterObject, int>();
        foreach (var entry in viewData.ExaminedPrisonerCharacters ?? new Dictionary<string, int>())
        {
            if (objectManager.TryGetObject<CharacterObject>(entry.Key, out var character))
                tracker._examinedPrisonerCharacterList[character] = entry.Value;
        }
        tracker._unexaminedFigureheads = GetObjects<Figurehead>(viewData.UnexaminedFigureheadIds);

        // Recounted against this player's prisoners on the next map refresh
        tracker.ClearPartyNotification();
        // Plunder from another party's raid in the server save
        tracker._plunderItems.Clear();

        var visualTrackerManager = Campaign.Current.VisualTrackerManager;
        // The server save's pins aren't this player's, but its quest and issue references stay
        foreach (var pinCount in GetPinCounts())
        {
            for (int i = 0; i < pinCount.Value; i++)
                visualTrackerManager.RemoveTrackedObject(pinCount.Key);
        }

        var savedPins = GetObjects<Settlement>(viewData.TrackedSettlementIds).Cast<ITrackableCampaignObject>()
            .Concat(GetObjects<MobileParty>(viewData.TrackedPartyIds));
        // Registered even when a quest already tracks it, so the pin keeps its own reference after the quest marker goes
        foreach (var pin in savedPins)
            visualTrackerManager.RegisterObject(pin);
    }

    public void SetPlayerViewData(string playerHeroId, PlayerViewData viewData)
    {
        ViewDataPlayerData.PlayerViewData[playerHeroId] = viewData;
    }

    // TrackerCount holds a reference per tracker, so the player's pins are the settlement and party references left after the quest and issue ones.
    // Clients never register the army target reference, ArmyPatches sets the army fields directly
    private Dictionary<ITrackableBase, int> GetPinCounts()
    {
        var gameReferences = new Dictionary<ITrackableBase, int>();
        void AddGameReferences(ITrackableBase trackedObject, int count)
        {
            gameReferences.TryGetValue(trackedObject, out var existing);
            gameReferences[trackedObject] = existing + count;
        }

        foreach (var questTracks in Campaign.Current.QuestManager._trackedObjects)
            AddGameReferences(questTracks.Key, questTracks.Value.Count(quest => quest.IsTrackEnabled));

        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            foreach (var trackedObject in issue._trackedObjects)
                AddGameReferences(trackedObject, 1);
        }

        var pinCounts = new Dictionary<ITrackableBase, int>();
        foreach (var tracked in Campaign.Current.VisualTrackerManager._trackedObjects)
        {
            if (!(tracked.Key is Settlement || tracked.Key is MobileParty)) continue;

            gameReferences.TryGetValue(tracked.Key, out var gameCount);
            var pinCount = tracked.Value.TrackerCount - gameCount;
            if (pinCount > 0) pinCounts[tracked.Key] = pinCount;
        }

        return pinCounts;
    }

    private List<string> GetIds<T>(IEnumerable<T> objects)
    {
        var ids = new List<string>();
        foreach (var obj in objects)
        {
            if (objectManager.TryGetId(obj, out var id))
                ids.Add(id);
        }

        return ids;
    }

    // Objects that no longer exist, like a dispersed army, are dropped
    private List<T> GetObjects<T>(List<string> ids) where T : class
    {
        var objects = new List<T>();
        foreach (var id in ids ?? new List<string>())
        {
            if (objectManager.TryGetObject<T>(id, out var obj))
                objects.Add(obj);
        }

        return objects;
    }
}
