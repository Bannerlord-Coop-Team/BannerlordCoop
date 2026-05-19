using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventComponents;

public class FieldBattleEventComponentTests : SyncTestBase
{
    public FieldBattleEventComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Server_MapEventParty_Properties()
    {
        string? componentId = null;
        TestEnvironment.Server.Call(() =>
        {
            DumpMapEventSetterPatches();
            DumpConstructorPatches<MapEventComponent>();
            DumpConstructorPatches<FieldBattleEventComponent>();

            var mapEvent = new MapEvent();
            var component = new FieldBattleEventComponent(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(component, out componentId));
        });

        Assert.NotNull(componentId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(componentId, out FieldBattleEventComponent component));
                Assert.NotNull(component);
                Assert.NotNull(component.MapEvent);
            });
        }
    }

    public static void DumpMapEventSetterPatches()
    {
        var setter = AccessTools.PropertySetter(
            typeof(MapEventComponent),
            nameof(MapEventComponent.MapEvent));

        if (setter == null)
        {
            Console.WriteLine("MapEvent setter was not found.");
            return;
        }

        Console.WriteLine($"Setter: {setter.FullDescription()}");

        var patchInfo = Harmony.GetPatchInfo(setter);

        if (patchInfo == null)
        {
            Console.WriteLine("No Harmony patches found on MapEvent setter.");
            return;
        }

        Console.WriteLine("Prefixes:");
        foreach (var prefix in patchInfo.Prefixes)
        {
            Console.WriteLine($"  owner={prefix.owner}, method={prefix.PatchMethod.FullDescription()}, priority={prefix.priority}");
        }

        Console.WriteLine("Postfixes:");
        foreach (var postfix in patchInfo.Postfixes)
        {
            Console.WriteLine($"  owner={postfix.owner}, method={postfix.PatchMethod.FullDescription()}, priority={postfix.priority}");
        }

        Console.WriteLine("Transpilers:");
        foreach (var transpiler in patchInfo.Transpilers)
        {
            Console.WriteLine($"  owner={transpiler.owner}, method={transpiler.PatchMethod.FullDescription()}, priority={transpiler.priority}");
        }
    }

    public static void DumpConstructorPatches<T>()
    {
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(T)))
        {
            Console.WriteLine($"Constructor: {ctor.FullDescription()}");

            var patchInfo = Harmony.GetPatchInfo(ctor);

            if (patchInfo == null)
            {
                Console.WriteLine("  No patches.");
                continue;
            }

            foreach (var prefix in patchInfo.Prefixes)
                Console.WriteLine($"  Prefix: owner={prefix.owner}, method={prefix.PatchMethod.FullDescription()}");

            foreach (var postfix in patchInfo.Postfixes)
                Console.WriteLine($"  Postfix: owner={postfix.owner}, method={postfix.PatchMethod.FullDescription()}");

            foreach (var transpiler in patchInfo.Transpilers)
                Console.WriteLine($"  Transpiler: owner={transpiler.owner}, method={transpiler.PatchMethod.FullDescription()}");
        }
    }
}
