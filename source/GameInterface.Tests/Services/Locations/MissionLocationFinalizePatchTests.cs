using Common.Util;
using GameInterface.Services.Locations.Patches;
using SandBox.Missions.MissionLogics;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

[Collection(nameof(MissionCurrentCollection))]
public sealed class MissionLocationFinalizePatchTests
{
    [Fact]
    public void FinalizeAfterSettlementEncounterWasCleared_DoesNotThrow()
    {
        Campaign previousCampaign = Campaign.Current;
        Campaign campaign = ObjectHelper.SkipConstructor<Campaign>();
        Campaign.Current = campaign;
        try
        {
            campaign.LocationEncounter = null;
            var logic = (MissionLocationLogic)FormatterServices.GetUninitializedObject(typeof(MissionLocationLogic));

            Assert.False(MissionLocationFinalizePatch.Prefix(logic));
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }
}
