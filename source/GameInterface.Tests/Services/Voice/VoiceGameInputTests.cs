using Common.Voice;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Voice;
using Moq;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

[Collection("Voice keybinding UI")]
public class VoiceGameInputTests
{
    private VoiceGameInput Create() => new VoiceGameInput(Mock.Of<IVoiceSceneSource>(), Mock.Of<IVoiceClock>(),
        Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>(), Mock.Of<IControllerIdProvider>(), Mock.Of<IChatService>(), Mock.Of<IVoiceWindowFocus>());

    [Theory]
    [InlineData(true, false, 1, true)]
    [InlineData(false, false, 9, false)]
    [InlineData(true, true, 9, false)]
    public void LivingParticipantUsesAgentButDeadOrLivingSpectatorUsesCamera(bool alive, bool spectator, float x, bool speak)
    {
        var point = Create().ScenePosition("scene:tournament", 7, new Vec3(1, 2, 3), new Vec3(9, 8, 7), alive, spectator);
        Assert.Equal(x, point.X);
        Assert.Equal(speak, point.CanSpeak);
        Assert.True(point.CanHear);
        Assert.Equal(7, point.Epoch);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    public void SettlementMenuUsesCampaignPositionUnlessInMissionBattleOrAnotherScreen(
        bool missionActive, bool isMapScreen, bool inBattle, bool audible)
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.Party.MobileParty = party;
        party._currentSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        party._position = new CampaignVec2(new Vec2(7, 8), true);
        if (inBattle)
            party.Party._mapEventSide = new MapEventSide(
                (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent)), BattleSideEnum.Attacker, party.Party);

        var point = Create().CampaignPosition(party, 7, missionActive, isMapScreen);

        Assert.Equal(audible ? VoicePosition.CampaignContext : "", point.Context);
        Assert.Equal(audible, point.CanSpeak);
        Assert.Equal(audible, point.CanHear);
        Assert.Equal(7, point.Epoch);
        if (audible)
        {
            Assert.Equal(7, point.X);
            Assert.Equal(8, point.Y);
        }
    }

    [Fact]
    public void DedicatedQBindingAndControllerWorkWithoutAnyVanillaContext()
    {
        var input = Create();
        var settings = new VoiceSettings();
        Assert.True(input.IsPushToTalkDown(settings, key => key == InputKey.Q));
        Assert.True(input.IsPushToTalkDown(settings, key => key == InputKey.ControllerLRight));
        settings.PushToTalkKey = InputKey.F12;
        Assert.False(input.IsPushToTalkDown(settings, key => key == InputKey.Q));
        Assert.True(input.IsPushToTalkDown(settings, key => key == InputKey.F12));
        Assert.True(input.IsPushToTalkDown(settings, key => key == InputKey.ControllerLRight));
        settings.PushToTalkKey = InputKey.V;
        Assert.True(input.IsPushToTalkDown(settings, key => key == InputKey.V));
        Assert.False(input.IsPushToTalkDown(settings, key => key == InputKey.Q));
    }

    [Fact]
    public void VanillaRemappingDoesNotAlterVoiceOrGetChangedByVoice()
    {
        var category = new CombatHotKeyCategory();
        var original = HotKeyManager.GetAllCategories().ToArray();
        try
        {
            HotKeyManager.RegisterInitialContexts(new[] { category });
            category.GetGameKey(33).KeyboardKey.ChangeKey(InputKey.F11);
            Assert.True(Create().IsPushToTalkDown(new VoiceSettings(), key => key == InputKey.Q));
            Assert.False(Create().IsPushToTalkDown(new VoiceSettings(), key => key == InputKey.F11));
            Assert.Equal(InputKey.F11, category.GetGameKey(33).KeyboardKey.InputKey);
        }
        finally { HotKeyManager.RegisterInitialContexts(original); }
    }
}
