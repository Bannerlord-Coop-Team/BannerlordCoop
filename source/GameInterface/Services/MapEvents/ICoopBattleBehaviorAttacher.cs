using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Attaches the coop P2P battle behaviors (the per-mission <c>CoopBattleController</c>) to a freshly opened
/// field-battle mission. Implemented in the Missions assembly — which knows the concrete behavior types — and
/// resolved from the shared container by the GameInterface battle flow, exactly like
/// <see cref="ICoopFieldBattleLauncher"/>: GameInterface cannot reference Missions directly (Missions depends
/// on GameInterface).
/// </summary>
public interface ICoopBattleBehaviorAttacher
{
    /// <summary>Create the per-mission coop battle behaviors and add them to <paramref name="mission"/>.</summary>
    void Attach(Mission mission);
}
