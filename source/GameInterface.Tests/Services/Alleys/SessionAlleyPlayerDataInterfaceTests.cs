using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Alleys;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.TroopRosters.Data;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Alleys;

public class SessionAlleyPlayerDataInterfaceTests
{
    [Fact]
    public void SetManagementData_PreservesLastRecruitTime()
    {
        const long lastRecruitTimeTicks = 12345;
        var existing = new AlleyManagementData("old-overseer", Array.Empty<TroopRosterElementData>())
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
            new[] { new TroopRosterElementData("troop", 3, 0, 0) });

        Assert.True(sessionInterface.TryGetManagementData("alley", out var updated));
        Assert.Equal("new-overseer", updated.OverseerId);
        Assert.Equal(lastRecruitTimeTicks, updated.LastRecruitTimeTicks);
    }

    [Fact]
    public void SetLastRecruitTimeTicks_UpdatesExistingEntry()
    {
        var sessionInterface = CreateInterface(new Dictionary<string, AlleyManagementData>
        {
            ["alley"] = new AlleyManagementData("overseer", Array.Empty<TroopRosterElementData>())
        });
        const long lastRecruitTimeTicks = 67890;

        sessionInterface.SetLastRecruitTimeTicks("alley", lastRecruitTimeTicks);

        Assert.True(sessionInterface.TryGetManagementData("alley", out var updated));
        Assert.Equal(lastRecruitTimeTicks, updated.LastRecruitTimeTicks);
    }

    private static SessionAlleyPlayerDataInterface CreateInterface(
        Dictionary<string, AlleyManagementData> managementData)
    {
        var coopSession = new Mock<ICoopSession>();
        coopSession.SetupGet(session => session.AlleyPlayerData)
            .Returns(new AlleyPlayerData(managementData));

        var provider = new Mock<ICoopSessionProvider>();
        provider.SetupGet(session => session.CoopSession).Returns(coopSession.Object);
        return new SessionAlleyPlayerDataInterface(provider.Object);
    }
}
