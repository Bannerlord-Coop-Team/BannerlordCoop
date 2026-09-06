using Common.Voice;
using GameInterface.Services.Chat;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Voice;

public interface IVoiceGameInput
{
    VoiceInputSnapshot Sample(long epoch, VoiceSettings settings);
}

public sealed class VoiceGameInput : IVoiceGameInput
{
    private readonly IVoiceSceneSource scenes;
    private readonly IVoiceClock clock;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IControllerIdProvider controller;
    private readonly IChatService chat;
    private readonly IVoiceWindowFocus window;

    public VoiceGameInput(IVoiceSceneSource scenes, IVoiceClock clock, IPlayerManager players,
        IObjectManager objects, IControllerIdProvider controller, IChatService chat, IVoiceWindowFocus window)
    {
        this.scenes = scenes;
        this.clock = clock;
        this.players = players;
        this.objects = objects;
        this.controller = controller;
        this.chat = chat;
        this.window = window;
    }

    public VoiceInputSnapshot Sample(long epoch, VoiceSettings settings)
    {
        bool focused = window.IsFocused;
        bool typing = chat.IsTyping || Input.IsOnScreenKeyboardActive ||
            (ScreenManager.FocusedLayer is GauntletLayer layer &&
             layer.UIContext.EventManager.FocusedWidget is EditableTextWidget);
        bool pressed = IsPushToTalkDown(settings, Input.IsKeyDown);
        var position = new VoicePosition("", epoch, 0, 0, 0, false, false);
        if (Campaign.Current != null && players.TryGetPlayer(controller.ControllerId, out var player) &&
            objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
        {
            if (Mission.Current != null && ScreenManager.TopScreen is MissionScreen screen)
            {
                var scene = scenes.GetScene();
                var agent = Mission.Current.MainAgent;
                bool alive = agent != null && agent.IsActive() && agent.Health > 0;
                if (!string.IsNullOrEmpty(scene.Context) && screen.CombatCamera != null)
                {
                    position = ScenePosition(scene.Context, epoch, alive ? agent.Position : Vec3.Zero,
                        screen.CombatCamera.Position, alive, scene.IsSpectator);
                }
            }
            else
            {
                position = CampaignPosition(party, epoch, Mission.Current != null, ScreenManager.TopScreen is MapScreen);
            }
        }
        return new VoiceInputSnapshot(position, focused, typing, pressed, clock.Milliseconds);
    }

    internal VoicePosition CampaignPosition(MobileParty party, long epoch, bool missionActive, bool isMapScreen)
    {
        if (missionActive || !isMapScreen || party.MapEvent != null)
            return new VoicePosition("", epoch, 0, 0, 0, false, false);

        var point = party.GetPosition2D;
        return new VoicePosition(VoicePosition.CampaignContext, epoch, point.X, point.Y, 0, true, true);
    }

    internal VoicePosition ScenePosition(string context, long epoch, Vec3 agent, Vec3 camera, bool alive, bool spectator)
    {
        bool speaking = alive && !spectator;
        var point = speaking ? agent : camera;
        return new VoicePosition(context, epoch, point.X, point.Y, point.Z, speaking, true);
    }

    internal bool IsPushToTalkDown(VoiceSettings settings, Func<InputKey, bool> isDown)
    {
        return isDown(settings.PushToTalkKey) || isDown(InputKey.ControllerLRight);
    }

}
