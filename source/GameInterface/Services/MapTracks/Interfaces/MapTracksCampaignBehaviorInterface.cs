using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.ObjectManager;
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

namespace GameInterface.Services.MapTracks.Interfaces;

public interface IMapTracksCampaignBehaviorInterface : IGameAbstraction
{
    /// <summary>
    /// Server method
    /// Update clients with changes to their visible tracks
    /// </summary>
    void PublishUpdateClientsMapTrackData(Dictionary<string, List<Track>> visibleTrackChanges, bool isRemovingTracks);

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
    /// Detect visible tracks for a single player party
    /// </summary>
    List<Track> DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty);

    /// <summary>
    /// Server method
    /// Replacement for vanilla implementation that uses nearest player party instead of only the main party's position
    /// </summary>
    bool IsTrackDropped(MapTracksCampaignBehavior behavior, MobileParty mobileParty);

    /// <summary>
    /// Server method
    /// Initialize a player's key in the dictionary
    /// </summary>
    void AddPlayerPartyKeys(string playerPartyId);

    /// <summary>
    /// Client method
    /// Use received changes from the server to update visible tracks cache
    /// </summary>
    void ApplyVisibleTrackChanges(MapTracksCampaignBehavior behavior, List<Track> visibleTrackChanges, bool IsRemovingTracks);
}

public class MapTracksCampaignBehaviorInterface : IMapTracksCampaignBehaviorInterface
{
    //private readonly PlayerMapTracksData playerMapTracksData = new(new());
    Dictionary<string, MBList<Track>> playerDetectedTracks = new();

    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;

    public MapTracksCampaignBehaviorInterface(
        IObjectManager objectManager,
        IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
    }

    public void PublishUpdateClientsMapTrackData(Dictionary<string, List<Track>> visibleTrackChanges, bool isRemovingTracks)
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

    private void RemoveExpiredTracks(MapTracksCampaignBehavior behavior)
    {
        var visibleTrackChanges = InitializeVisibleTrackChanges();

        for (int i = behavior._allTracks.Count - 1; i >= 0; i--)
        {
            Track track = behavior._allTracks[i];
            if (track.IsExpired)
            {
                behavior._allTracks.Remove(track);

                // Do this for every player
                foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
                {
                    if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) continue;
                    if (!playerDetectedTracks.ContainsKey(playerPartyId)) continue;

                    if (playerDetectedTracks[playerPartyId].Contains(track))
                    {
                        playerDetectedTracks[playerPartyId].Remove(track);

                        visibleTrackChanges[playerPartyId].Add(track);
                    }
                }
            }
        }

        PublishUpdateClientsMapTrackData(visibleTrackChanges, true);

        // Reset and clear tracks after sending updated data to clients
        // Otherwise comparison won't match as party name and culture get reset
        for (int i = behavior._allTracks.Count - 1; i >= 0; i--)
        {
            Track track = behavior._allTracks[i];
            if (track.IsExpired)
            {
                behavior._trackLocator.RemoveLocatable(track);
                behavior._trackPool.ReleaseTrack(track);
            }
        }
    }

    public void QuarterHourlyTick(MapTracksCampaignBehavior behavior)
    {
        var visibleTrackChanges = InitializeVisibleTrackChanges();

        // Run for all player parties instead of just one
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) continue;
            if (!playerDetectedTracks.ContainsKey(playerPartyId)) continue;

            if (!playerParty.Party.IsValid) continue;

            visibleTrackChanges[playerPartyId] = DetectTracksForPlayerParty(behavior, playerParty);
        }

        PublishUpdateClientsMapTrackData(visibleTrackChanges, false);
    }

    public List<Track> DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty)
    {
        var visibleTrackChanges = new List<Track>();

        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) return visibleTrackChanges;

        int effectiveScoutingSkill = (playerParty.EffectiveScout != null) ? playerParty.EffectiveScout.GetSkillValue(DefaultSkills.Scouting) : 0;
        if (effectiveScoutingSkill != 0)
        {
            float maxTrackSpottingDistanceForPlayerParty = GetMaxTrackSpottingDistanceForPlayerParty(playerParty);
            LocatableSearchData<Track> locatableSearchData = behavior._trackLocator.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), maxTrackSpottingDistanceForPlayerParty);
            for (Track track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData); track != null; track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData))
            {
                if (!IsDetectedByPlayerParty(playerPartyId, track) && behavior._allTracks.Contains(track) && GetTrackDetectionDifficultyForPlayerParty(playerParty, track, maxTrackSpottingDistanceForPlayerParty) < (float)effectiveScoutingSkill)
                {
                    if (!playerDetectedTracks.ContainsKey(playerPartyId)) continue;

                    playerDetectedTracks[playerPartyId].Add(track);
                    SkillLevelingManager.OnTrackDetected(track);

                    visibleTrackChanges.Add(track);
                }
            }
        }

        return visibleTrackChanges;
    }

    public bool IsTrackDropped(MapTracksCampaignBehavior behavior, MobileParty mobileParty)
    {
        float skipTrackChance = Campaign.Current.Models.MapTrackModel.GetSkipTrackChance(mobileParty);
        if (MBRandom.RandomFloat < skipTrackChance)
        {
            return false;
        }

        // Find the closest party to determine if the track should be dropped
        // Safe to use server MainParty as baseline as MainParty.IsActive is false
        MobileParty closestParty = MobileParty.MainParty;
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (mobileParty.Position.DistanceSquared(playerParty.Position) < mobileParty.Position.DistanceSquared(closestParty.Position))
            {
                closestParty = playerParty;
            }
        }

        float closestPlayerPartyDistance = mobileParty.Position.DistanceSquared(closestParty.Position);
        float num2 = closestParty.IsActive ? (closestParty._lastCalculatedSpeed * Campaign.Current.Models.MapTrackModel.MaxTrackLife) : 0f;
        return num2 * num2 > closestPlayerPartyDistance;
    }

    public void AddPlayerPartyKeys(string playerPartyId)
    {
        // If a player rejoins, their visible tracks will be lost. Reset server side detected tracks
        playerDetectedTracks[playerPartyId] = new MBList<Track>();
    }

    public void ApplyVisibleTrackChanges(MapTracksCampaignBehavior behavior, List<Track> visibleTrackChanges, bool IsRemovingTracks)
    {
        foreach (var changedTrack in visibleTrackChanges)
        {
            if (IsRemovingTracks) // Delete expired track
            {
                // Find a matching track to delete. Done this way instead of registering every track to reduce network traffic
                if (!FindMatchingTrack(behavior, changedTrack, out var targetTrack)) continue;

                behavior._detectedTracksCache.Remove(targetTrack);
                CampaignEventDispatcher.Instance.TrackLost(targetTrack);
            }
            else // Detect new track
            {
                changedTrack.IsDetected = true;
                behavior._detectedTracksCache.Add(changedTrack);

                // Party is lost in transfer so unable to determine map faction from the track
                // IsEnemy only appears at a high scouting skill level and has very minimal effect on gameplay
                //track.IsEnemy = FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, party.MapFaction);
                CampaignEventDispatcher.Instance.TrackDetected(changedTrack);
            }
        }
    }

    private bool IsDetectedByPlayerParty(string playerPartyId, Track targetTrack)
    {
        foreach (var track in playerDetectedTracks[playerPartyId])
        {
            if (track == targetTrack)
                return true;
        }
        return false;
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
               a.NumberOfMenWithHorse == b.NumberOfMenWithHorse &&
               a.NumberOfMenWithoutHorse == b.NumberOfMenWithoutHorse &&
               a.NumberOfPackAnimals == b.NumberOfPackAnimals &&
               a.NumberOfPrisoners == b.NumberOfPrisoners &&
               a.CreationTime == b.CreationTime &&
               a.Life == b.Life &&
               a.PartyType == b.PartyType;
    }

    private Dictionary<string, List<Track>> InitializeVisibleTrackChanges()
    {
        var visibleTrackChanges = new Dictionary<string, List<Track>>();
        foreach (var playerPartyId in playerDetectedTracks.Keys)
        {
            visibleTrackChanges[playerPartyId] = new();
        }
        return visibleTrackChanges;
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
        float num2 = -75f + elapsedHoursUntilNow / Campaign.Current.Models.MapTrackModel.MaxTrackLife * 100f + num * 100f + MathF.Max(0f, 100f - (float)size) * (CampaignTime.Now.IsNightTime ? 10f : 1f);
        if (playerParty.HasPerk(DefaultPerks.Scouting.Ranger, true) && !playerParty.IsCurrentlyAtSea)
        {
            num2 -= num2 * DefaultPerks.Scouting.Ranger.SecondaryBonus;
        }
        return num2;
    }
}