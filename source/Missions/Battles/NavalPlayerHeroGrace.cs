using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Services.Network;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Vanilla's main-agent grace on a side whose hulls are all out of action, kept for each side's player heroes, so in PvP
/// neither side loses its hulls' fight while one of its players still fights or died less than 20 s ago.
/// </summary>
public interface INavalPlayerHeroGrace
{
    /// <summary>
    /// [Game thread] Records each side with a live hero of a current mission member among <paramref name="agents"/> at mission
    /// time <paramref name="now"/>; an offline player's hero fielded as AI holds nothing.
    /// </summary>
    void Observe(IEnumerable<Agent> agents, float now);

    /// <summary>
    /// Whether <paramref name="side"/>'s hulls out of action may defeat it at <paramref name="now"/>: no live member hero of it
    /// was seen here, as for an AI side, or none for more than <see cref="NavalPlayerHeroGrace.HeroConsideredDeadSeconds"/>.
    /// </summary>
    bool HasExpired(BattleSideEnum side, float now);
}

/// <inheritdoc cref="INavalPlayerHeroGrace"/>
public class NavalPlayerHeroGrace : INavalPlayerHeroGrace
{
    /// <summary>Vanilla's <c>NavalBattleEndLogic.MainAgentConsideredDeadDuration</c>.</summary>
    public const float HeroConsideredDeadSeconds = 20f;

    private readonly IBattleSession session;
    private readonly IMissionContext missionContext;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly Dictionary<BattleSideEnum, float> lastLiveHeroAt = new Dictionary<BattleSideEnum, float>();

    // Takes the battle's own member and peers, and the managers that name each player's hero character.
    public NavalPlayerHeroGrace(IBattleSession session, IMissionContext missionContext, IPlayerManager playerManager,
        IObjectManager objectManager)
    {
        this.session = session;
        this.missionContext = missionContext;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    // Reads each live human's side and character for the member hero check.
    public void Observe(IEnumerable<Agent> agents, float now) =>
        ObserveLiveHumans(agents
            .Where(agent => agent.IsHuman && agent.IsActive() && agent.Team != null)
            .Select(agent => (agent.Team.Side, agent.Character)), now);

    public bool HasExpired(BattleSideEnum side, float now) =>
        !lastLiveHeroAt.TryGetValue(side, out float seenAt) || now - seenAt > HeroConsideredDeadSeconds;

    /// <summary>
    /// Records the side of each live human whose character is a current member's hero character, the object its player's
    /// <c>CharacterObjectId</c> (a registry id, not the StringId) resolves to.
    /// </summary>
    internal void ObserveLiveHumans(IEnumerable<(BattleSideEnum Side, BasicCharacterObject Character)> liveHumans, float now)
    {
        var heroes = MemberHeroCharacters();
        if (heroes.Count == 0) return;

        foreach (var (side, character) in liveHumans)
        {
            if (heroes.Any(hero => ReferenceEquals(hero, character))) lastLiveHeroAt[side] = now;
        }
    }

    // The hero characters of this client and every other member, resolved once per scan.
    private List<CharacterObject> MemberHeroCharacters()
    {
        var heroes = new List<CharacterObject>();
        foreach (var controllerId in missionContext.ControllersInMission.Append(session.OwnControllerId))
        {
            if (controllerId != null && playerManager.TryGetPlayer(controllerId, out var player)
                && player.CharacterObjectId != null && objectManager.TryGetObject(player.CharacterObjectId, out CharacterObject hero))
                heroes.Add(hero);
        }

        return heroes;
    }
}
