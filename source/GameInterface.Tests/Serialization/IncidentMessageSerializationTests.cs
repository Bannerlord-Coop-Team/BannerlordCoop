using GameInterface.Services.Incidents.Messages;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class IncidentMessageSerializationTests
{
    [Fact]
    public void IncidentInvoked_PreservesIncidentIdAndSeed()
    {
        // A real seed is CampaignTime.Now.NumTicks, which is well past int range
        long seed = 9_876_543_210_123L;

        var copy = Serializer.DeepClone(new NetworkIncidentInvoked("Incident_incident_trade_proposal", seed));

        Assert.Equal("Incident_incident_trade_proposal", copy.IncidentId);
        Assert.Equal(seed, copy.ActiveIncidentSeed);
    }

    [Fact]
    public void IncidentOptionInvoked_PreservesIdsAndOptionTextId()
    {
        var copy = Serializer.DeepClone(new NetworkIncidentOptionInvoked("Hero_Player1", "Party_Player1", "Incident_incident_arrow_proofing", "CNYKy7q3"));

        Assert.Equal("Hero_Player1", copy.MainHeroId);
        Assert.Equal("Party_Player1", copy.MainPartyId);
        Assert.Equal("Incident_incident_arrow_proofing", copy.IncidentId);
        Assert.Equal("CNYKy7q3", copy.OptionTextId);
    }

    [Fact]
    public void IncidentDismissed_PreservesIncidentId()
    {
        var copy = Serializer.DeepClone(new NetworkIncidentDismissed("Incident_incident_arrow_proofing"));

        Assert.Equal("Incident_incident_arrow_proofing", copy.IncidentId);
    }

    [Fact]
    public void InitializeServerIncidentsDataKeys_PreservesPlayerHeroId()
    {
        var copy = Serializer.DeepClone(new NetworkInitializeServerIncidentsDataKeys("Hero_Player1"));

        Assert.Equal("Hero_Player1", copy.PlayerHeroId);
    }

    [Fact]
    public void GameMenuOptionSelected_PreservesMenuAndOptionIds()
    {
        var copy = Serializer.DeepClone(new NetworkIncidentsGameMenuOptionSelected("Hero_Player1", "Party_Player1", "town", "town_leave"));

        Assert.Equal("Hero_Player1", copy.MainHeroId);
        Assert.Equal("Party_Player1", copy.MainPartyId);
        Assert.Equal("town", copy.GameMenuId);
        Assert.Equal("town_leave", copy.OptionId);
    }
}
