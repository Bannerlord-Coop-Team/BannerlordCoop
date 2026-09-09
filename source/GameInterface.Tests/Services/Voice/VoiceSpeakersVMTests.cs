using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

[Collection("Voice keybinding UI")]
public class VoiceSpeakersVMTests
{
    [Fact]
    public void MovieBindsPassiveLeftSideHeroNameList()
    {
        var document = System.Xml.Linq.XDocument.Load(
            global::GameInterface.Tests.Services.UI.PopupUIMovieBindingTests.FindMoviePath("CoopVoiceSpeakers.xml"));
        var root = document.Descendants("Widget").First();
        Assert.Equal("true", root.Attribute("DoNotAcceptEvents")?.Value);
        Assert.Equal("true", root.Attribute("DoNotPassEventsToChildren")?.Value);
        var list = document.Descendants("ListPanel").First();
        Assert.Equal("Left", list.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("@HasSpeakers", list.Attribute("IsVisible")?.Value);
        Assert.Contains(document.Descendants("ListPanel"), item => item.Attribute("DataSource")?.Value == "{Speakers}");
        Assert.Contains(document.Descendants("TextWidget"), item => item.Attribute("Text")?.Value == "@Name");
    }

    [Fact]
    public void OnlyAudibleResolvedHeroNamesAreShownAndInactiveRowsAreRemoved()
    {
        var names = new Mock<IVoiceSpeakerNameResolver>();
        names.Setup(x => x.Resolve("platform:1")).Returns("Liena");
        names.Setup(x => x.Resolve("platform:2")).Returns("Ira");
        var vm = new VoiceSpeakersVM(names.Object);
        vm.Refresh(new[] { "platform:1", "unknown", "platform:2" });
        Assert.Equal(new[] { "Liena", "Ira" }, vm.Speakers.Select(x => x.Name));
        vm.Refresh(new[] { "platform:2" });
        Assert.Equal("Ira", Assert.Single(vm.Speakers).Name);
        vm.Refresh(Array.Empty<string>());
        Assert.False(vm.HasSpeakers);
    }

    [Fact]
    public void ResolverUsesHeroNameNotPlatformIdAndOmitsSelfAndMissingRegistryEntries()
    {
        var players = new Mock<IPlayerManager>();
        var objects = new Mock<IObjectManager>();
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("self");
        var player = new Player("platform:1", "hero", "party", "clan", "character");
        players.Setup(x => x.TryGetPlayer("platform:1", out player)).Returns(true);
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero._name = new TextObject("Liena");
        objects.Setup(x => x.TryGetObject("hero", out hero)).Returns(true);
        var resolver = new VoiceSpeakerNameResolver(players.Object, objects.Object, controller.Object);
        Assert.Equal("Liena", resolver.Resolve("platform:1"));
        Assert.Null(resolver.Resolve("unknown"));
        Assert.Null(resolver.Resolve("self"));
    }
}
