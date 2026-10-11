using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// Registers one War Sails hull with a single upgrade slot on the server and every client, and creates ships of it on the server.
/// </summary>
public abstract class ShipTestBase : SyncTestBase
{
    internal const string HullId = "ShipHull_coop_test_hull";
    internal const string SlotTag = "sail";
    internal const float HullMaxHitPoints = 1000f;
    internal const float HullMaxSailHitPoints = 500f;

    protected ShipTestBase(ITestOutputHelper output) : base(output)
    {
        foreach (var instance in AllInstances)
        {
            RegisterHull(instance);
        }
    }

    internal IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(Clients);

    internal string CreateServerShip()
    {
        string? shipId = null;
        Server.Call(() =>
        {
            var ship = new Ship(Server.GetRegisteredObject<ShipHull>(HullId));
            Assert.True(Server.ObjectManager.TryGetId(ship, out shipId));
        });

        Assert.NotNull(shipId);
        return shipId!;
    }

    private static void RegisterHull(EnvironmentInstance instance)
    {
        var hull = instance.CreateRegisteredObject<ShipHull>(HullId);
        hull.AvailableSlots = new MBReadOnlyDictionary<string, ShipSlot>(new Dictionary<string, ShipSlot>
        {
            { SlotTag, ObjectHelper.SkipConstructor<ShipSlot>() },
        });
        hull.MaxHitPoints = (int)HullMaxHitPoints;
        hull.MaxSailHitPoints = (int)HullMaxSailHitPoints;
        hull.CanEquipFigurehead = true;
    }
}
