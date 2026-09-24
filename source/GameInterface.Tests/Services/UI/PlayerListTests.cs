using GameInterface.Services.Players.Data;
using GameInterface.Services.Chat;
using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.UITab;
using TaleWorlds.InputSystem;
using Moq;
using GameInterface.Services.UI.PlayerList;
using ProtoBuf;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects player-list identity, offline presentation and wire/save metadata.</summary>
public class PlayerListTests
{
    // Reconnects reuse the existing UI row even when players have the same display name.
    [Fact]
    public void ReconnectPreservesRowAndOfflineClearsActivity()
    {
        var vm = new PlayerListVM(() => { });
        var first = new PlayerListEntry { ControllerId = "one", PlatformName = "Same name", HeroName = "Hero", Online = true, Activity = PlayerActivity.Siege };
        var second = first with { ControllerId = "two" };
        vm.Update(new[] { first, second });
        Assert.Equal("Players Online: 2", vm.OnlineSummary);
        Assert.Equal(348f, vm.PanelHeight);
        Assert.False(vm.Rows[0].IsAlternate);
        Assert.True(vm.Rows[1].IsAlternate);
        var row = vm.Rows[0];
        vm.Update(new[] { first with { Online = false }, second });
        Assert.Equal("Offline", row.Status);
        Assert.True(row.IsOffline);
        Assert.Equal("Offline", row.PresenceText);
        Assert.Equal("Players Online: 1", vm.OnlineSummary);
        Assert.Equal(0.55f, row.RowOpacity);
        Assert.Equal(@"SPGeneral\GameMenu\leave_icon", row.ActivityIcon);
        vm.Update(new[] { first with { Activity = PlayerActivity.Town }, second });
        Assert.Equal(2, vm.Rows.Count);
        Assert.Same(row, vm.Rows[0]);
        Assert.Equal("In Town", row.Status);
        Assert.True(row.IsOnline);
        Assert.False(row.IsOffline);
        Assert.Equal("Online", row.PresenceText);
        Assert.Equal("Players Online: 2", vm.OnlineSummary);
        Assert.Equal(1f, row.RowOpacity);
        Assert.Equal(@"SPGeneral\GameMenu\visit_town_icon", row.ActivityIcon);
    }

    // Preserves all display fields across the network and platform names in existing player saves.
    [Fact]
    public void SnapshotAndPlayerMetadataRoundTrip()
    {
        var entry = new PlayerListEntry { ControllerId = "one", PlatformName = "玩家", HeroName = "Hero", Online = true, Activity = PlayerActivity.Hideout };
        var message = Serializer.DeepClone(new NetworkPlayerList(new[] { entry }));
        Assert.Equal(entry, Assert.Single(message.Entries));
        var player = new Player("one", "hero", "party", "clan", "character") { PlatformName = "玩家" };
        Assert.Equal(player.PlatformName, Serializer.DeepClone(player).PlatformName);
        Assert.Equal(player.PlatformName,
            JsonConvert.DeserializeObject<Player>(JsonConvert.SerializeObject(player))!.PlatformName);
        Assert.Null(Serializer.DeepClone(new Player("old", "hero", "party", "clan", "character")).PlatformName);
    }

    // Long names and offscreen rows remain in the data source rather than being shortened or capped.
    [Fact]
    public void LargeRosterPreservesFullNamesAndRemovesDeletedRegistrations()
    {
        var vm = new PlayerListVM(() => { });
        var name = new string('W', 160);
        var entries = Enumerable.Range(0, 32).Select(index => new PlayerListEntry
        {
            ControllerId = index.ToString(), PlatformName = name, HeroName = name
        }).ToArray();
        vm.Update(entries);
        Assert.Equal(32, vm.Rows.Count);
        Assert.Equal(684f, vm.PanelHeight);
        Assert.All(vm.Rows, row => Assert.Equal(name, row.PlatformName));
        vm.Update(entries.Skip(1).ToArray());
        Assert.Equal(31, vm.Rows.Count);
        Assert.False(vm.Rows[0].IsAlternate);
        Assert.True(vm.Rows[1].IsAlternate);
        Assert.DoesNotContain(vm.Rows, row => row.ControllerId == "0");
    }

#if DEBUG
    // Layout previews leave incoming snapshots intact and restore the latest real roster when disabled.
    [Fact]
    public void PreviewRestoresLatestReceivedRoster()
    {
        var store = new Mock<ICoopOptionsStore>();
        store.Setup(x => x.LoadOrDefault()).Returns(new CoopOptionsData());
        using var service = new PlayerListService(Mock.Of<IChatService>(), store.Object, Mock.Of<IMessageBroker>());
        service.PreviewLayout(true);
        service.Update(new[] { new PlayerListEntry { ControllerId = "real", PlatformName = "Actual player" } });
        Assert.Contains("Preview Player 18", service.Describe());
        service.PreviewLayout(false);
        Assert.Contains("Actual player", service.Describe());
        Assert.DoesNotContain("Preview", service.Describe());
    }
#endif

    // A changed binding stays local until Apply and is persisted with the UI settings.
    [Fact]
    public void PlayerListBindingIsSavedOnlyOnApply()
    {
        var options = new CoopOptionsData();
        var broker = new Mock<IMessageBroker>();
        var section = new UISection(options, broker.Object);
        Assert.Equal(InputKey.O, section.PlayerListKey.CurrentKey.InputKey);
        section.PlayerListKey.Set(InputKey.F8);
        Assert.Equal(InputKey.O, UIOptionsTabProvider.GetPlayerListKey(options));
        section.Apply(UIOptionsTabProvider.TabId, options);
        Assert.Equal(InputKey.F8, UIOptionsTabProvider.GetPlayerListKey(options));
        section.PlayerListKey.Set(InputKey.Escape);
        Assert.Equal(InputKey.F8, section.PlayerListKey.CurrentKey.InputKey);
        section.OnFinalize();
    }

    // Missing assignment is explicit and never masquerades as an idle campaign hero.
    [Fact]
    public void UnassignedHeroDisplaysConnecting()
    {
        var row = new PlayerListRowVM(new PlayerListEntry { ControllerId = "one", Online = true });
        Assert.Equal("Hero not assigned", row.HeroName);
        Assert.Equal("Connecting", row.Status);
    }
}
