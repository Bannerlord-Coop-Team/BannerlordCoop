using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Voice;

public interface IVoiceSpeakerNameResolver
{
    string Resolve(string controllerId);
}

public sealed class VoiceSpeakerNameResolver : IVoiceSpeakerNameResolver
{
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IControllerIdProvider controller;

    public VoiceSpeakerNameResolver(IPlayerManager players, IObjectManager objects, IControllerIdProvider controller)
    {
        this.players = players;
        this.objects = objects;
        this.controller = controller;
    }

    // Called only by the presentation tick, never by the poller or audio worker.
    public string Resolve(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId) || controllerId == controller.ControllerId ||
            !players.TryGetPlayer(controllerId, out var player) ||
            !objects.TryGetObject<Hero>(player.HeroId, out var hero)) return null;
        return hero.Name?.ToString();
    }
}
