using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEngineMissiles
{
    public class SiegeEngineMissileSyncTests : SyncTestBase
    {
        string siegeEngineMissileId;
        public SiegeEngineMissileSyncTests(ITestOutputHelper output) : base(output)
        {
            siegeEngineMissileId = TestEnvironment.CreateRegisteredObject<SiegeEvent.SiegeEngineMissile>();
        }
        [Fact]
        public void Server_Settlement_Fields()
        {
            Server.ObjectManager.TryGetObject(siegeEngineMissileId, out SiegeEvent.SiegeEngineMissile siegeEngineMissile);


            //TestEnvironment.AssertField<SiegeEvent.SiegeEngineMissile,int>(nameof(SiegeEvent.SiegeEngineMissile.ShooterSlotIndex), 2);
            //TestEnvironment.AssertReferenceField<SiegeEvent.SiegeEngineMissile, SiegeEngineType>(nameof(SiegeEvent.SiegeEngineMissile.ShooterSiegeEngineType));

        }

    }
}
