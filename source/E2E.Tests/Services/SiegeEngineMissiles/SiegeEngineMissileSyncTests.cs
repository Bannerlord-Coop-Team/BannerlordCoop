using E2E.Tests.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
using static TaleWorlds.MountAndBlade.Mission;

namespace E2E.Tests.Services.SiegeEngineMissiles
{
    public class SiegeEngineMissileSyncTests : SyncTestBase
    {
        string siegeEngineMissileId;
        string siegeEngineTypeId;
        string siegeEngineConstructionProgressId;
        public SiegeEngineMissileSyncTests(ITestOutputHelper output) : base(output)
        {
            siegeEngineMissileId = TestEnvironment.CreateRegisteredObject<SiegeEvent.SiegeEngineMissile>();
            siegeEngineTypeId = TestEnvironment.CreateRegisteredObject<SiegeEngineType>();
            siegeEngineConstructionProgressId = TestEnvironment.CreateRegisteredObject<SiegeEvent.SiegeEngineConstructionProgress>();
        }
        [Fact]
        public void Server_Settlement_Fields()
        {
            Server.ObjectManager.TryGetObject(siegeEngineMissileId, out SiegeEvent.SiegeEngineMissile siegeEngineMissile);

            TestEnvironment.AssertReferenceField<SiegeEvent.SiegeEngineMissile, SiegeEngineType>(nameof(SiegeEvent.SiegeEngineMissile.ShooterSiegeEngineType));
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, int>(nameof(SiegeEvent.SiegeEngineMissile.ShooterSlotIndex), 2);
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, SiegeBombardTargets>(nameof(SiegeEvent.SiegeEngineMissile.TargetType), SiegeBombardTargets.Wall);
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, int>(nameof(SiegeEvent.SiegeEngineMissile.TargetSlotIndex), 2);
            TestEnvironment.AssertReferenceField<SiegeEvent.SiegeEngineMissile, SiegeEvent.SiegeEngineConstructionProgress>(nameof(SiegeEvent.SiegeEngineMissile.TargetSiegeEngine));
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, bool>(nameof(SiegeEvent.SiegeEngineMissile.HitSuccessful), false);
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, CampaignTime>(nameof(SiegeEvent.SiegeEngineMissile.CollisionTime), CampaignTime.Now);
            TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile, CampaignTime>(nameof(SiegeEvent.SiegeEngineMissile.FireDecisionTime), CampaignTime.Now);
        }

    }
}
