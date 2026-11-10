using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

internal class PoachersBattleMissionInitializer : IBattleMissionInitializer
{
    public int Priority => 200;

    public bool CanHandle(MapEvent mapEvent) => FindQuest(mapEvent) != null;

    public MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
    {
        var quest = FindQuest(mapEvent);
        if (quest == null)
            throw new InvalidOperationException("The battle does not contain an active quest's poachers party");

        var scene = quest._questVillage.Settlement.LocationComplex.GetScene("village_center", 1);
        if (string.IsNullOrEmpty(scene))
            throw new InvalidOperationException("The poachers quest village has no battle scene");

        return new MissionInitializerRecord(scene)
        {
            DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier(),
            DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier(),
            TerrainType = (int)Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mapEvent.Position.Face),
            SceneLevels = "",
            DoNotUseLoadingScreen = false,
            DecalAtlasGroup = (int)DecalAtlasGroup.Battle,
            RandomTerrainSeed = randomTerrainSeed,
            PlayingInCampaignMode = true,
            AtmosphereOnCampaign = atmosphereOnCampaign,
        };
    }

    private static Quest FindQuest(MapEvent mapEvent)
    {
        if (mapEvent == null) return null;

        return Campaign.Current.IssueManager.Issues.Values.Select(issue => issue.IssueQuest).OfType<Quest>().FirstOrDefault(quest =>
            quest.IsOngoing && quest._poachersParty != null && quest._poachersParty.MapEvent == mapEvent);
    }
}
