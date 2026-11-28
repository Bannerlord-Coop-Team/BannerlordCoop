using Common.Util;
using Coop.Core.Server.Services.Time.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using SandBox.View.Map;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Time;

/// <summary>
/// Clicking the map to travel resumes time the way vanilla's map click does.
/// </summary>
public class MapClickTimeControlTests : IDisposable
{
    private const string ControllerId = "MapClickPlayer";

    private readonly E2ETestEnvironment environment;
    private CampaignTimeControlMode? previousTimeMode;
    private readonly ModOptions previousOptions = ModConfigProvider.ModOptions;

    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Client => environment.Clients.First();

    public MapClickTimeControlTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        ModConfigProvider.ModOptions = previousOptions;

        // Server and clients share the static mode the TimeControlMode getter reads, so put it back for later tests.
        if (previousTimeMode is CampaignTimeControlMode mode)
            Server.Call(() => TimePatches.OverrideTimeControlMode(mode));

        environment.Dispose();
    }

    [Fact]
    public void MapClick_WhilePaused_ResumesTimeOnServerAndClients()
    {
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { MapClickChangesTime = true });
        ConnectPlayer();
        PauseServer();

        // HandleClickTimeChange reads BannerlordConfig, whose static initializer asks the native layer for the system language
        var disabledMethods = new[] { AccessTools.Method(typeof(TaleWorlds.Engine.Utilities), nameof(TaleWorlds.Engine.Utilities.GetSystemLanguage)) };

        // HandleClickTimeChange is the vanilla helper MapScreen's map and parley clicks call after a travel order
        Client.Call(() =>
        {
            var mapScreen = (MapScreen)RuntimeHelpers.GetUninitializedObject(typeof(MapScreen));
            mapScreen.HandleClickTimeChange(isDoubleClick: false);

            Assert.False(AllowedThread.IsThisThreadAllowed());
        }, disabledMethods);

        Server.Call(() => Assert.Equal(TimeControlEnum.Play_1x, Server.Resolve<ITimeControlInterface>().GetTimeControl()));
        foreach (var client in environment.Clients)
        {
            var change = Assert.Single(client.InternalMessages.GetMessages<NetworkChangeTimeControlMode>());
            Assert.Equal(TimeControlEnum.Play_1x, change.NewControlMode);
        }
    }

    // A connected player keeps the no-players unpause policy from holding the pause.
    private void ConnectPlayer()
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out string characterId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out string clanId));

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player(ControllerId, heroId, partyId, clanId, characterId)));
        });

        environment.ConnectRegisteredPlayer(Client, ControllerId);
    }

    private void PauseServer()
    {
        Server.Call(() =>
        {
            previousTimeMode ??= Campaign.Current.TimeControlMode;
            Server.Resolve<ITimeControlInterface>().ServerSetTimeControl(TimeControlEnum.Pause);
            Assert.Equal(TimeControlEnum.Pause, Server.Resolve<ITimeControlInterface>().GetTimeControl());
        });

        foreach (var client in environment.Clients)
        {
            client.InternalMessages.Clear();
        }
    }
}
