using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.MapTracks.Data;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.ObjectManager;
using Helpers;
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
    void PublishUpdateClientsMapTrackData();
    void OnHourlyTick(MapTracksCampaignBehavior behavior);
    void QuarterHourlyTick(MapTracksCampaignBehavior behavior);
    bool DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty);
    void AddPlayerPartyKeys(string playerPartyId);
}

public class MapTracksCampaignBehaviorInterface : IMapTracksCampaignBehaviorInterface
{
    private readonly PlayerMapTracksData playerMapTracksData = new(new());
    private readonly IObjectManager objectManager;
    private readonly IMessageBroker messageBroker;

    public MapTracksCampaignBehaviorInterface(
        IObjectManager objectManager,
        IMessageBroker messageBroker)
    {
        this.objectManager = objectManager;
        this.messageBroker = messageBroker;
    }

    public void PublishUpdateClientsMapTrackData()
    {
        messageBroker.Publish(this, new UpdateClientsMapTrackData(playerMapTracksData));
    }

    public void OnHourlyTick(MapTracksCampaignBehavior behavior)
    {
        var shouldUpdateClients = false;
        for (int i = behavior._allTracks.Count - 1; i >= 0; i--)
        {
            Track track = behavior._allTracks[i];
            if (track.IsExpired)
            {
                behavior._allTracks.Remove(track);

                // Do this for every player and send update to all players
                foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
                {
                    if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) continue;

                    if (!playerMapTracksData.PlayerDetectedTracks.ContainsKey(playerPartyId)) continue;

                    if (playerMapTracksData.PlayerDetectedTracks[playerPartyId].Contains(track))
                    {
                        playerMapTracksData.PlayerDetectedTracks[playerPartyId].Remove(track);
                        shouldUpdateClients = true;
                    }
                }

                behavior._trackLocator.RemoveLocatable(track);
                behavior._trackPool.ReleaseTrack(track);
            }
        }

        if (shouldUpdateClients)
        {
            PublishUpdateClientsMapTrackData();
        }
    }

    public void QuarterHourlyTick(MapTracksCampaignBehavior behavior)
    {
        bool shouldUpdateClients = false;
        // Run for all player parties instead of just the one
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (!playerParty.Party.IsValid) continue;

            shouldUpdateClients = DetectTracksForPlayerParty(behavior, playerParty);
        }

        if (shouldUpdateClients)
        {
            PublishUpdateClientsMapTrackData();
        }
    }

    // Needs to also be called when a client joins the game
    public bool DetectTracksForPlayerParty(MapTracksCampaignBehavior behavior, MobileParty playerParty)
    {
        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId)) return false;

        bool shouldUpdateClients = false;
        int effectiveScoutingSkill = (playerParty.EffectiveScout != null) ? playerParty.EffectiveScout.GetSkillValue(DefaultSkills.Scouting) : 0;
        if (effectiveScoutingSkill != 0)
        {
            float maxTrackSpottingDistanceForPlayerParty = GetMaxTrackSpottingDistanceForPlayerParty(playerParty);
            LocatableSearchData<Track> locatableSearchData = behavior._trackLocator.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), maxTrackSpottingDistanceForPlayerParty);
            for (Track track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData); track != null; track = behavior._trackLocator.FindNextLocatable(ref locatableSearchData))
            {
                if (!track.IsDetected && behavior._allTracks.Contains(track) && GetTrackDetectionDifficultyForPlayerParty(playerParty, track, maxTrackSpottingDistanceForPlayerParty) < (float)effectiveScoutingSkill)
                {
                    if (!playerMapTracksData.PlayerDetectedTracks.ContainsKey(playerPartyId)) continue;

                    playerMapTracksData.PlayerDetectedTracks[playerPartyId].Add(track);
                    SkillLevelingManager.OnTrackDetected(track);

                    shouldUpdateClients = true;
                }
            }
        }

        return shouldUpdateClients;
    }

    public void AddPlayerPartyKeys(string playerPartyId)
    {
        if (playerMapTracksData.PlayerDetectedTracks.ContainsKey(playerPartyId)) return;

        playerMapTracksData.PlayerDetectedTracks[playerPartyId] = new MBList<Track>();
    }

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