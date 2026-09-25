using System;
using System.Linq;
using System.Collections.Generic;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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

/// <summary>Shows audible speaker names and their campaign-party voice icons.</summary>
public sealed class VoiceSpeakingOverlay : GlobalLayer, IVoiceSpeakingOverlay
{
    private readonly IVoiceClient voice;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IControllerIdProvider controller;
    private readonly VoiceSpeakersVM viewModel;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private float elapsed;

    // Uses session voice and player services to attach audible speakers to campaign parties.
    public VoiceSpeakingOverlay(IVoiceClient voice, IVoiceSpeakerNameResolver names,
        IPlayerManager players, IObjectManager objects, IControllerIdProvider controller)
    {
        this.voice = voice;
        this.players = players;
        this.objects = objects;
        this.controller = controller;
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

    // Updates the talking list and party icons only while an unobstructed gameplay screen is active.
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
            ClearPartyMarkers();
            if (viewModel.HasSpeakers) viewModel.Refresh(Array.Empty<string>());
            return;
        }
        UpdatePartyMarkers(ScreenManager.TopScreen as MapScreen);
        if (elapsed < 0.05f) return;
        elapsed = 0;
        viewModel.Refresh(voice.AudibleSpeakers ?? Array.Empty<string>());
    }

    // Projects audible parties every frame so icons follow movement and camera changes.
    private void UpdatePartyMarkers(MapScreen map)
    {
        if (map?.MapCameraView?.Camera == null)
        {
            ClearPartyMarkers();
            return;
        }
        var speakers = new HashSet<string>(voice.AudibleSpeakers);
        if (voice.IsTransmitting) speakers.Add(controller.ControllerId);
        foreach (var marker in viewModel.PartyMarkers.ToArray())
        {
            if (speakers.Contains(marker.ControllerId)) continue;
            viewModel.PartyMarkers.Remove(marker);
            marker.OnFinalize();
        }
        foreach (string speaker in speakers)
        {
            var marker = viewModel.PartyMarkers.FirstOrDefault(item => item.ControllerId == speaker);
            if (!players.TryGetPlayer(speaker, out var player) ||
                !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
            {
                marker?.UpdatePosition(0f, 0f, false);
                continue;
            }
            if (marker == null)
            {
                marker = new VoicePartyMarkerVM(speaker);
                viewModel.PartyMarkers.Add(marker);
            }
            var position = (party.Position + party.EventPositionAdder).AsVec3() + new Vec3(0f, 0f, 0.8f);
            float x = 0f, y = 0f, depth = 0f;
            MBWindowManager.WorldToScreenInsideUsableArea(map.MapCameraView.Camera, position, ref x, ref y, ref depth);
            marker.UpdatePosition(x * gauntletLayer.UIContext.InverseScale, y * gauntletLayer.UIContext.InverseScale,
                depth > 0f && depth < 100f && map.MapCameraView.Camera.Position.z < 200f && (party.IsMainParty || party.IsVisible));
        }
    }

    // Releases party markers on screen changes and overlay shutdown.
    private void ClearPartyMarkers()
    {
        foreach (var marker in viewModel.PartyMarkers) marker.OnFinalize();
        viewModel.PartyMarkers.Clear();
    }

    // Removes the global overlay and its view models when the session ends.
    public void Dispose()
    {
        if (gauntletLayer == null) return;
        gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        ClearPartyMarkers();
        viewModel.OnFinalize();
        gauntletLayer = null;
        movie = null;
        Layer = null;
    }
}
