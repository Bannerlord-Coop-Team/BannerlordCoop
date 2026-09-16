using System;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Voice;

public interface IVoiceSpeakingOverlay : IDisposable
{
    void Initialize();
}

public sealed class VoiceSpeakingOverlay : GlobalLayer, IVoiceSpeakingOverlay
{
    private readonly IVoiceClient voice;
    private readonly VoiceSpeakersVM viewModel;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private float elapsed;

    public VoiceSpeakingOverlay(IVoiceClient voice, IVoiceSpeakerNameResolver names)
    {
        this.voice = voice;
        viewModel = new VoiceSpeakersVM(names);
    }

    public void Initialize()
    {
        if (gauntletLayer != null) return;
        gauntletLayer = new GauntletLayer("CoopVoiceSpeakers", 109);
        gauntletLayer.InputRestrictions.ResetInputRestrictions();
        movie = gauntletLayer.LoadMovie("CoopVoiceSpeakers", viewModel);
        Layer = gauntletLayer;
        ScreenManager.AddGlobalLayer(this, false);
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        bool gameplay = false;
        ScreenLayer scene = null;
        if (ScreenManager.TopScreen is MapScreen map)
        {
            scene = map.SceneLayer;
            gameplay = GameStateManager.Current?.ActiveState is MapState state && !state.AtMenu;
        }
        else if (ScreenManager.TopScreen is MissionScreen mission)
        {
            scene = mission.SceneLayer;
            gameplay = !mission.IsPhotoModeEnabled && !mission.IsConversationActive;
        }
        bool visible = gameplay && !LoadingWindow.IsLoadingWindowActive &&
            Campaign.Current?.ConversationManager?.IsConversationInProgress != true &&
            ReferenceEquals(ScreenManager.FocusedLayer, scene);
        if (gauntletLayer.IsActive != visible) ScreenManager.SetSuspendLayer(gauntletLayer, !visible);
        elapsed += dt;
        if (!visible)
        {
            if (viewModel.HasSpeakers) viewModel.Refresh(Array.Empty<string>());
            return;
        }
        if (elapsed < 0.05f) return;
        elapsed = 0;
        viewModel.Refresh(voice.AudibleSpeakers ?? Array.Empty<string>());
    }

    public void Dispose()
    {
        if (gauntletLayer == null) return;
        gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        viewModel.OnFinalize();
        gauntletLayer = null;
        movie = null;
        Layer = null;
    }
}
