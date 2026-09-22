using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Alleys;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace GameInterface.Tests.Services.Alleys;

public class SessionAlleyPlayerDataInterfaceTests
{
    [Fact]
    public void SetManagementData_PreservesLastRecruitTime()
    {
        const long lastRecruitTimeTicks = 12345;
        var existing = new AlleyManagementData("old-overseer", Array.Empty<AlleyRosterElementData>())
        {
            LastRecruitTimeTicks = lastRecruitTimeTicks
        };
        var sessionInterface = CreateInterface(new Dictionary<string, AlleyManagementData>
        {
            ["alley"] = existing
        });

        sessionInterface.SetManagementData(
            "alley",
            "new-overseer",
            Array.Empty<TroopRosterElementData>());

        Assert.True(sessionInterface.TryGetManagementData("alley", out var updated));
        Assert.Equal("new-overseer", updated.OverseerId);
        Assert.Equal(lastRecruitTimeTicks, updated.LastRecruitTimeTicks);
    }

    [Fact]
    public void SetLastRecruitTimeTicks_UpdatesExistingEntry()
    {
        var sessionInterface = CreateInterface(new Dictionary<string, AlleyManagementData>
        {
            ["alley"] = new AlleyManagementData("overseer", Array.Empty<AlleyRosterElementData>())
        });
        const long lastRecruitTimeTicks = 67890;

        sessionInterface.SetLastRecruitTimeTicks("alley", lastRecruitTimeTicks);

        Assert.True(sessionInterface.TryGetManagementData("alley", out var updated));
        Assert.Equal(lastRecruitTimeTicks, updated.LastRecruitTimeTicks);
    }

    [Fact]
    public void ExistingStringBackedGarrisonJson_RoundTrips()
    {
        const string json = "{\"OverseerId\":\"hero\",\"Garrison\":[{\"CharacterId\":\"CharacterObject_troop\",\"Number\":3,\"WoundedNumber\":1,\"Xp\":42}]}";
        var options = new JsonSerializerOptions { IncludeFields = true };

        var data = JsonSerializer.Deserialize<AlleyManagementData>(json, options);

        Assert.Equal("CharacterObject_troop", data.Garrison[0].CharacterId);
        Assert.Equal(3, data.Garrison[0].Number);
        Assert.Contains(
            "\"CharacterId\":\"CharacterObject_troop\"",
            JsonSerializer.Serialize(data.Garrison[0], options));
    }

    private static SessionAlleyPlayerDataInterface CreateInterface(
        Dictionary<string, AlleyManagementData> managementData)
    {
        var coopSession = new Mock<ICoopSession>();
        coopSession.SetupGet(session => session.AlleyPlayerData)
            .Returns(new AlleyPlayerData(managementData));

        var provider = new Mock<ICoopSessionProvider>();
        provider.SetupGet(session => session.CoopSession).Returns(coopSession.Object);
        return new SessionAlleyPlayerDataInterface(provider.Object, Mock.Of<IObjectManager>());
    }
}
