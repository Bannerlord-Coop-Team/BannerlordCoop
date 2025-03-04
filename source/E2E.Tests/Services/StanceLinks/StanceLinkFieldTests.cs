using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using System.Reflection;
using Common.Util;
using HarmonyLib;
using GameInterface.AutoSync;

namespace E2E.Tests.Services.StanceLinks;
public class StanceLinkFieldsTests : SyncTestBase
{
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public StanceLinkFieldsTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<StanceLink>();
    }

    [Fact]
    public void ServerStanceLink_SyncAll_Fields()
    {
        // Arrange
        TestEnvironment.AssertField<StanceLink, StanceType>(nameof(StanceLink._stanceType), StanceType.Alliance);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink.BehaviorPriority), 3);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._casualties1), 69);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._casualties2), 42);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._dailyTributeFrom1To2), 666);
        TestEnvironment.AssertField<StanceLink, bool>(nameof(StanceLink._isAtConstantWar), true);
        TestEnvironment.AssertField<StanceLink, CampaignTime>(nameof(StanceLink._peaceDeclarationDate), new CampaignTime(999L));
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._successfulRaids1), 123);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._successfulRaids2), 456);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._successfulSieges1), 789);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._successfulSieges2), 12);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._totalTributePaidby1), 421);
        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._totalTributePaidby2), 1);
        TestEnvironment.AssertField<StanceLink, CampaignTime>(nameof(StanceLink._warStartDate), new CampaignTime(456789L));
    }




}