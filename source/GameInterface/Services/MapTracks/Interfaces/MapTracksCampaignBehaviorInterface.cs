using Common;
using Common.Messaging;
using GameInterface.Services.MapTracks.Data;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapTracks.Interfaces;

public interface IMapTracksCampaignBehaviorInterface : IGameAbstraction
{
    /// <summary>
    /// Server method
    /// Update clients with changes to their visible tracks
    /// </summary>
    void PublishUpdateClientsMapTrackData(Dictionary<string, List<MapTrackData>> visibleTrackChanges, bool isRemovingTracks);

    /// <summary>
    /// Server method
    /// Removes expired tracks
    /// </summary>
    void OnHourlyTick(MapTracksCampaignBehavior behavior);

    /// <summary>
    /// Server method
    /// Spots new tracks
    /// </summary>
    void QuarterHourlyTick(MapTracksCampaignBehavior behavior);

    /// <summary>
    /// Server method
    /// Replacement for vanilla implementation that does not depend on the host having a main party or hero
    /// </summary>
    void AddTrack(MapTracksCampaignBehavior behavior, MobileParty party, CampaignVec2 trackPosition, Vec2 trackDirection);

    /// <summary>
    /// Server method
    /// Also save faction data for map tracks.
    /// This isn't needed in vanilla as IsEnemy is normally stored in each track but this needs to be unique per player in coop
    /// </summary>
    void SyncTrackMapFactions(MapTracksCampaignBehavior behavior, IDataStore dataStore);

    /// <summary>
    /// Server method
    /// Save player detected tracks unique per player. Vanilla only ever assumes one player.
    /// </summary>
    void SyncPlayerDetectedTracks(MapTracksCampaignBehavior behavior, IDataStore dataStore);

    /// <summary>
    /// Server method
    /// Detect visible tracks for a single player party, granting scouting xp based on argument
    /// </summary>
    List<MapTrackData> DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty, bool grantScoutingXp = true);

    /// <summary>
    /// Server method
    /// Get a player's full visible set of tracks on joining, without treating any of them as a new detection
    /// </summary>
    List<MapTrackData> InitializePlayerVisibleTracks(MapTracksCampaignBehavior behavior, MobileParty playerParty);

    /// <summary>
    /// Server method
    /// Replacement for vanilla implementation that checks every player party's own spotting radius
    /// </summary>
    bool IsTrackDropped(MapTracksCampaignBehavior behavior, MobileParty mobileParty);

    /// <summary>
    /// Server method
    /// Initialize a player's key in the dictionary. Returns false when the player already had one.
    /// </summary>
    bool AddPlayerPartyKeys(string playerPartyId);

    /// <summary>
    /// Client method
    /// Use received changes from the server to update visible tracks cache
    /// </summary>
    void ApplyVisibleTrackChanges(MapTracksCampaignBehavior behavior, List<MapTrackData> visibleTrackChanges, bool isRemovingTracks);

    /// <summary>
    /// Client method
    /// Removes every visible track, used when the server re-sends a full set of visible tracks
    /// </summary>
    void ClearVisibleTracks(MapTracksCampaignBehavior behavior);
}

public class MapTracksCampaignBehaviorInterface : IMapTracksCampaignBehaviorInterface
{
    private readonly Dictionary<string, HashSet<Track>> playerDetectedTracks = new();

    // When tracks are dropped the original party is lost.
    // Keep track of the faction for computing IsEnemy on clients.
    private readonly Dictionary<Track, IFaction> trackMapFactions = new();

    private const string TrackMapFactionsSaveKey = "Coop_TrackMapFactions";
    private const string PlayerDetectedTracksSaveKey = "Coop_PlayerDetectedTracks";

    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;

    public MapTracksCampaignBehaviorInterface(
        IObjectManager objectManager,
        IMessageBroker messageBroker,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
    }

    public void PublishUpdateClientsMapTrackData(Dictionary<string, List<MapTrackData>> visibleTrackChanges, bool isRemovingTracks)
    {
        if (visibleTrackChanges.Values.Any(list => list.Count > 0))
        {
            messageBroker.Publish(this, new UpdateClientsMapTrackData(visibleTrackChanges, isRemovingTracks));
        }
    }

    public void OnHourlyTick(MapTracksCampaignBehavior behavior)
    {
        RemoveExpiredTracks(behavior);
    }

    public void QuarterHourlyTick(MapTracksCampaignBehavior behavior)
    {
        var visibleTrackChanges = InitializeVisibleTrackChanges();

        // Run for all player parties instead of just one
        foreach (var playerParty in GetPlayerParties())
        {
            // Don't update tracks for disconnected players
            if (playerManager.IsOwnerOfPartyDisconnected(playerParty)) continue;

            if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) continue;
            if (!playerDetectedTracks.ContainsKey(playerPartyId)) continue;

            visibleTrackChanges[playerPartyId] = DetectTracksForPlayerParty(behavior, playerParty);
        }

        PublishUpdateClientsMapTrackData(visibleTrackChanges, false);
    }

    public void AddTrack(MapTracksCampaignBehavior behavior, MobileParty party, CampaignVec2 trackPosition, Vec2 trackDirection)
    {
        Track track = behavior._trackPool._stack.Count > 0 ? behavior._trackPool._stack.Pop() : new Track();

        int numberOfAllMembers = party.Party.NumberOfAllMembers;
        int numberOfHealthyMembers = party.Party.NumberOfHealthyMembers;
        int numberOfMenWithHorse = party.Party.NumberOfMenWithHorse;
        int numberOfMenWithoutHorse = party.Party.NumberOfMenWithoutHorse;
        int numberOfPackAnimals = party.Party.NumberOfPackAnimals;
        int numberOfPrisoners = party.Party.NumberOfPrisoners;

        TextObject partyName = party.Name;
        if (party.Army != null && party.Army.LeaderParty == party)
        {
            partyName = party.ArmyName;
            foreach (MobileParty attachedParty in party.Army.LeaderParty.AttachedParties)
            {
                numberOfAllMembers += attachedParty.Party.NumberOfAllMembers;
                numberOfHealthyMembers += attachedParty.Party.NumberOfHealthyMembers;
                numberOfMenWithHorse += attachedParty.Party.NumberOfMenWithHorse;
                numberOfMenWithoutHorse += attachedParty.Party.NumberOfMenWithoutHorse;
                numberOfPackAnimals += attachedParty.Party.NumberOfPackAnimals;
                numberOfPrisoners += attachedParty.Party.NumberOfPrisoners;
            }
        }

        track.Position = trackPosition;
        track.Direction = trackDirection.RotationInRadians;
        track.PartyType = Track.GetPartyTypeEnum(party);
        track.PartyName = partyName;
        track.Culture = party.Party?.Culture;
        track.Speed = party.Speed;
        track.Life = GetTrackLife(party);
        track.NumberOfAllMembers = numberOfAllMembers;
        track.NumberOfHealthyMembers = numberOfHealthyMembers;
        track.NumberOfMenWithHorse = numberOfMenWithHorse;
        track.NumberOfMenWithoutHorse = numberOfMenWithoutHorse;
        track.NumberOfPackAnimals = numberOfPackAnimals;
        track.NumberOfPrisoners = numberOfPrisoners;
        track.IsPointer = false;
        track.IsDetected = false;

        // Resolve on clients based on faction
        track.IsEnemy = false;
        track.CreationTime = CampaignTime.Now;

        // Store track in mapFactions data so clients can resolve IsEnemy when receiving track
        trackMapFactions[track] = party.MapFaction;

        behavior._allTracks.Add(track);
        behavior._trackLocator.UpdateLocator(track);
    }

    public void SyncTrackMapFactions(MapTracksCampaignBehavior behavior, IDataStore dataStore)
    {
        // Loaded on clients too. Load empty data, the server tracks map track faction data.
        var savedTrackMapFactions = ModInformation.IsClient
            ? new List<TrackMapFactionSaveData>()
            : BuildTrackMapFactionSaveData(behavior);

        dataStore.SyncData(TrackMapFactionsSaveKey, ref savedTrackMapFactions);

        if (!dataStore.IsLoading) return;

        trackMapFactions.Clear();

        // Don't load data on clients
        // Also return early if not on a save written before this record existed
        if (ModInformation.IsClient || savedTrackMapFactions == null) return;

        // Load valid map tracks back into trackMapFactions
        foreach (var savedTrackMapFaction in savedTrackMapFactions)
        {
            if (savedTrackMapFaction?.Track == null || savedTrackMapFaction.MapFaction == null) continue;

            // Skip expired tracks matching OnGameLoadFinished removing from _allTracks
            if (savedTrackMapFaction.Track.IsExpired) continue;

            trackMapFactions[savedTrackMapFaction.Track] = savedTrackMapFaction.MapFaction;
        }
    }

    public void SyncPlayerDetectedTracks(MapTracksCampaignBehavior behavior, IDataStore dataStore)
    {
        // Loaded on clients too. Load empty data, the server tracks all player detected track data
        var savedPlayerDetectedTracks = ModInformation.IsClient
            ? new List<PlayerDetectedTracksSaveData>()
            : BuildPlayerDetectedTracks();

        dataStore.SyncData(PlayerDetectedTracksSaveKey, ref savedPlayerDetectedTracks);

        if (!dataStore.IsLoading) return;

        playerDetectedTracks.Clear();

        // Don't load data on clients
        // Also return early if not on a save written before this record existed
        if (ModInformation.IsClient || savedPlayerDetectedTracks == null) return;

        // Load detected tracks back into playerDetectedTracks
        foreach (var savedPlayerDetectedTrack in savedPlayerDetectedTracks)
        {
            if (savedPlayerDetectedTrack?.PlayerId == null || savedPlayerDetectedTrack.DetectedTracks == null) continue;

            playerDetectedTracks[savedPlayerDetectedTrack.PlayerId] = new();
            foreach (var detectedTrack in savedPlayerDetectedTrack.DetectedTracks)
            {
                playerDetectedTracks[savedPlayerDetectedTrack.PlayerId].Add(detectedTrack);
            }
        }
    }

    private List<TrackMapFactionSaveData> BuildTrackMapFactionSaveData(MapTracksCampaignBehavior behavior)
    {
        var savedTrackMapFactions = new List<TrackMapFactionSaveData>();

        foreach (var track in behavior._allTracks)
        {
            if (!trackMapFactions.TryGetValue(track, out var mapFaction) || mapFaction == null) continue;

            savedTrackMapFactions.Add(new TrackMapFactionSaveData(track, mapFaction));
        }

        return savedTrackMapFactions;
    }

    private List<PlayerDetectedTracksSaveData> BuildPlayerDetectedTracks()
    {
        var savedPlayerDetectedTracks = new List<PlayerDetectedTracksSaveData>();

        foreach (var playerDetectedTracks in playerDetectedTracks)
        {
            savedPlayerDetectedTracks.Add(new PlayerDetectedTracksSaveData(playerDetectedTracks.Key, playerDetectedTracks.Value.ToList()));
        }

        return savedPlayerDetectedTracks;
    }

    public List<MapTrackData> DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty, bool grantScoutingXp = true)
    {
        var visibleTrackChanges = new List<MapTrackData>();

        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) return visibleTrackChanges;

        if (!playerDetectedTracks.TryGetValue(playerPartyId, out var detectedTracks)) return visibleTrackChanges;
        if (!playerParty.Party.IsValid) return visibleTrackChanges;

        int effectiveScoutingSkill = (playerParty.EffectiveScout != null) ? playerParty.EffectiveScout.GetSkillValue(DefaultSkills.Scouting) : 0;
        if (effectiveScoutingSkill != 0)
        {
            float maxTrackSpottingDistanceForPlayerParty = GetMaxTrackSpottingDistanceForPlayerParty(playerParty);
            LocatableSearchData<Track> locatableSearchData = behavior._trackLocator.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), maxTrackSpottingDistanceForPlayerParty);
            for (Track track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData); track != null; track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData))
            {
                if (!detectedTracks.Contains(track) && behavior._allTracks.Contains(track) && GetTrackDetectionDifficultyForPlayerParty(playerParty, track, maxTrackSpottingDistanceForPlayerParty) < (float)effectiveScoutingSkill)
                {
                    detectedTracks.Add(track);

                    if (grantScoutingXp) GrantTrackDetectionXp(playerParty, track);

                    visibleTrackChanges.Add(ToMapTrackData(track));
                }
            }
        }

        return visibleTrackChanges;
    }

    public List<MapTrackData> InitializePlayerVisibleTracks(MapTracksCampaignBehavior behavior, MobileParty playerParty)
    {
        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) return new List<MapTrackData>();

        // Use existing tracks if present
        if (!playerDetectedTracks.TryGetValue(playerPartyId, out var detectedTracks)) return new List<MapTrackData>();

        return detectedTracks.Select(ToMapTrackData).ToList();
    }

    public bool IsTrackDropped(MapTracksCampaignBehavior behavior, MobileParty mobileParty)
    {
        float skipTrackChance = Campaign.Current.Models.MapTrackModel.GetSkipTrackChance(mobileParty);
        if (MBRandom.RandomFloat < skipTrackChance)
        {
            return false;
        }

        // Vanilla's radius is derived from the observing party's own speed, so the geometrically
        // closest player is not the deciding one: a stationary party can be nearest while a faster
        // party slightly further out still reaches the track. Drop it if any player qualifies.
        foreach (var playerParty in GetPlayerParties())
        {
            if (!playerParty.IsActive) continue;

            float playerPartyDistance = mobileParty.Position.DistanceSquared(playerParty.Position);
            float trackReach = playerParty._lastCalculatedSpeed * Campaign.Current.Models.MapTrackModel.MaxTrackLife;

            if (trackReach * trackReach > playerPartyDistance) return true;
        }

        // No player close enough to drop a track for
        return false;
    }

    public bool AddPlayerPartyKeys(string playerPartyId)
    {
        if (playerDetectedTracks.ContainsKey(playerPartyId)) return false;

        playerDetectedTracks[playerPartyId] = new HashSet<Track>();
        return true;
    }

    public void ApplyVisibleTrackChanges(MapTracksCampaignBehavior behavior, List<MapTrackData> visibleTrackChanges, bool isRemovingTracks)
    {
        foreach (var changedTrackData in visibleTrackChanges)
        {
            var changedTrack = changedTrackData.Track;
            if (changedTrack == null) continue;

            if (isRemovingTracks) // Delete expired track
            {
                // Find a matching track to delete. Done this way instead of registering every track to reduce network traffic
                if (!FindMatchingTrack(behavior, changedTrack, out var targetTrack)) continue;

                behavior._detectedTracksCache.Remove(targetTrack);
                CampaignEventDispatcher.Instance.TrackLost(targetTrack);
            }
            else // Detect new track
            {
                if (changedTrack.Culture == null && !changedTrack.IsPointer) continue;

                // Resolved here rather than on the server
                changedTrack.IsEnemy = IsTrackHostileToMainParty(changedTrackData.MapFactionId);
                changedTrack.IsDetected = true;
                behavior._detectedTracksCache.Add(changedTrack);

                CampaignEventDispatcher.Instance.TrackDetected(changedTrack);
            }
        }
    }

    public void ClearVisibleTracks(MapTracksCampaignBehavior behavior)
    {
        foreach (var track in behavior._detectedTracksCache.ToList())
        {
            behavior._detectedTracksCache.Remove(track);
            CampaignEventDispatcher.Instance.TrackLost(track);
        }
    }

    private void RemoveExpiredTracks(MapTracksCampaignBehavior behavior)
    {
        var visibleTrackChanges = InitializeVisibleTrackChanges();

        var expiredTracks = new List<Track>();
        var playerParties = GetPlayerParties();

        for (int i = behavior._allTracks.Count - 1; i >= 0; i--)
        {
            Track track = behavior._allTracks[i];
            if (!track.IsExpired) continue;

            behavior._allTracks.RemoveAt(i);
            expiredTracks.Add(track);

            // Do this for every player
            foreach (var playerParty in playerParties)
            {
                if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) continue;
                if (!playerDetectedTracks.TryGetValue(playerPartyId, out var detectedTracks)) continue;

                if (detectedTracks.Remove(track))
                {
                    // Removals are matched on the client by field comparison, so don't need to compare source faction
                    visibleTrackChanges[playerPartyId].Add(new MapTrackData(track, null));
                }
            }
        }

        PublishUpdateClientsMapTrackData(visibleTrackChanges, true);

        // Reset and clear tracks after sending updated data to clients
        // Otherwise comparison won't match as party name and culture get reset
        foreach (var expiredTrack in expiredTracks)
        {
            behavior._trackLocator.RemoveLocatable(expiredTrack);
            behavior._trackPool.ReleaseTrack(expiredTrack);
            trackMapFactions.Remove(expiredTrack);
        }
    }

    private MapTrackData ToMapTrackData(Track track)
    {
        if (!trackMapFactions.TryGetValue(track, out var mapFaction) || mapFaction == null) return new MapTrackData(track, null);
        if (!objectManager.TryGetId(mapFaction, out var mapFactionId)) return new MapTrackData(track, null);

        return new MapTrackData(track, mapFactionId);
    }

    private bool IsTrackHostileToMainParty(string mapFactionId)
    {
        if (Hero.MainHero?.MapFaction == null) return false;
        if (!TryResolveFaction(mapFactionId, out var trackMapFaction)) return false;

        return FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, trackMapFaction);
    }

    private List<MobileParty> GetPlayerParties()
    {
        var playerParties = new List<MobileParty>();
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty)) continue;

            playerParties.Add(playerParty);
        }

        return playerParties;
    }

    private bool FindMatchingTrack(MapTracksCampaignBehavior behavior, Track inputTrack, out Track outputTrack)
    {
        outputTrack = null;

        foreach (var existingTrack in behavior._detectedTracksCache)
        {
            if (TracksMatch(inputTrack, existingTrack))
            {
                outputTrack = existingTrack;
                return true;
            }
        }

        return false;
    }

    private static bool TracksMatch(Track a, Track b)
    {
        return a.Position == b.Position &&
               a.Direction == b.Direction &&
               a.PartyName == b.PartyName &&
               a.Culture == b.Culture &&
               a.Speed == b.Speed &&
               a.NumberOfAllMembers == b.NumberOfAllMembers &&
               a.NumberOfHealthyMembers == b.NumberOfHealthyMembers &&
               a.NumberOfMenWithHorse == b.NumberOfMenWithHorse &&
               a.NumberOfMenWithoutHorse == b.NumberOfMenWithoutHorse &&
               a.NumberOfPackAnimals == b.NumberOfPackAnimals &&
               a.NumberOfPrisoners == b.NumberOfPrisoners &&
               a.CreationTime == b.CreationTime &&
               a.Life == b.Life &&
               a.PartyType == b.PartyType &&
               a.IsPointer == b.IsPointer;
    }

    private Dictionary<string, List<MapTrackData>> InitializeVisibleTrackChanges()
    {
        var visibleTrackChanges = new Dictionary<string, List<MapTrackData>>();
        foreach (var playerPartyId in playerDetectedTracks.Keys)
        {
            visibleTrackChanges[playerPartyId] = new();
        }
        return visibleTrackChanges;
    }

    private void GrantTrackDetectionXp(MobileParty playerParty, Track track)
    {
        bool previousIsEnemy = track.IsEnemy;
        track.IsEnemy = IsTrackHostileTo(playerParty, track);

        try
        {
            float scoutingXp = Campaign.Current.Models.MapTrackModel.GetSkillFromTrackDetected(track);
            playerParty.EffectiveScout?.AddSkillXp(DefaultSkills.Scouting, scoutingXp);
        }
        finally
        {
            track.IsEnemy = previousIsEnemy;
        }
    }

    private bool IsTrackHostileTo(MobileParty playerParty, Track track)
    {
        if (playerParty.MapFaction == null) return false;
        if (!trackMapFactions.TryGetValue(track, out var trackMapFaction) || trackMapFaction == null) return false;

        return FactionManager.IsAtWarAgainstFaction(playerParty.MapFaction, trackMapFaction);
    }

    // Replacement for GetMaxTrackSpottingDistanceForMainParty to work for any player party instead of just MobileParty.MainParty
    private float GetMaxTrackSpottingDistanceForPlayerParty(MobileParty playerParty)
    {
        ExplainedNumber explainedNumber = new ExplainedNumber(0f, false, null);
        SkillHelper.AddSkillBonusForParty(DefaultSkillEffects.TrackingRadius, playerParty, ref explainedNumber);
        if (!playerParty.IsCurrentlyAtSea)
        {
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.Ranger, playerParty, true, ref explainedNumber, false);
        }
        return explainedNumber.ResultNumber;
    }

    // Replacement for GetTrackDetectionDifficultyForMainParty to work for any player party instead of just MobileParty.MainParty
    private float GetTrackDetectionDifficultyForPlayerParty(MobileParty playerParty, Track track, float trackSpottingDistance)
    {
        int size = track.Size;
        float elapsedHoursUntilNow = track.CreationTime.ElapsedHoursUntilNow;
        float num = (track.Position.ToVec2() - playerParty.Position.ToVec2()).Length / trackSpottingDistance;
        float num2 = -75f + (elapsedHoursUntilNow / Campaign.Current.Models.MapTrackModel.MaxTrackLife * 100f) + (num * 100f) + (MathF.Max(0f, 100f - (float)size) * (CampaignTime.Now.IsNightTime ? 10f : 1f));
        if (playerParty.HasPerk(DefaultPerks.Scouting.Ranger, true) && !playerParty.IsCurrentlyAtSea)
        {
            num2 -= num2 * DefaultPerks.Scouting.Ranger.SecondaryBonus;
        }
        return num2;
    }

    // Replacement for DefaultMapTrackModel.GetTrackLife to work for any player party instead of just MobileParty.MainParty
    // Apply the tracking perk when any player holds it so it doesn't do nothing.
    private float GetTrackLife(MobileParty party)
    {
        bool isOnSnow = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace) == TerrainType.Snow;
        int partySize = party.MemberRoster.TotalManCount + party.PrisonRoster.TotalManCount;
        float lifeRatio = MathF.Min(1f, ((0.5f * MBRandom.RandomFloat) + 0.5f + ((float)partySize * 0.007f)) / 2f) * (isOnSnow ? 0.5f : 1f);

        if (!party.IsCurrentlyAtSea && GetPlayerParties().Any(playerParty => playerParty.HasPerk(DefaultPerks.Scouting.Tracker)))
        {
            lifeRatio = MathF.Min(1f, lifeRatio * (1f + DefaultPerks.Scouting.Tracker.PrimaryBonus));
        }

        return MathF.Round(Campaign.Current.Models.MapTrackModel.MaxTrackLife * lifeRatio);
    }

    private bool TryResolveFaction(string mapFactionId, out IFaction faction)
    {
        faction = null;

        if (string.IsNullOrEmpty(mapFactionId)) return false;

        if (objectManager.TryGetObject<Kingdom>(mapFactionId, out var kingdom))
        {
            faction = kingdom;
            return true;
        }

        if (objectManager.TryGetObject<Clan>(mapFactionId, out var clan))
        {
            faction = clan;
            return true;
        }

        return false;
    }
}