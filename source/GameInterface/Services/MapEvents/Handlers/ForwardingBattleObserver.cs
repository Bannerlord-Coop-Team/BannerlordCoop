using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Stand-in <see cref="IBattleObserver"/> attached to a map event while the server runs an
/// auto-resolve simulation on a client's behalf. It records each <see cref="TroopNumberChanged"/>
/// callback (resolving the party and character to network ids) so the driving handler can flush a
/// round's worth of changes to the client for paced playback. Other observer callbacks are not
/// needed for the scoreboard's troop counts and are ignored.
/// </summary>
internal class ForwardingBattleObserver : IBattleObserver
{
    private readonly IObjectManager objectManager;
    private readonly List<BattleSimTroopChange> currentRound = new();

    public ForwardingBattleObserver(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    /// <summary>Take and clear the changes accumulated since the last flush.</summary>
    public BattleSimTroopChange[] FlushRound()
    {
        var changes = currentRound.ToArray();
        currentRound.Clear();
        return changes;
    }

    public void TroopNumberChanged(BattleSideEnum side, IBattleCombatant battleCombatant, BasicCharacterObject character,
        int number = 0, int numberKilled = 0, int numberWounded = 0, int numberRouted = 0, int killCount = 0, int numberReadyToUpgrade = 0)
    {
        if (!(battleCombatant is PartyBase party))
            return;

        if (!objectManager.TryGetId(party, out var partyId))
            return;

        var characterObject = character as CharacterObject;
        var isHero = characterObject?.IsHero == true;
        var objectToResolve = isHero ? (object)characterObject.HeroObject : characterObject;

        if (objectToResolve == null || !objectManager.TryGetId(objectToResolve, out var characterId))
            return;

        currentRound.Add(new BattleSimTroopChange(
            (int)side, partyId, characterId, isHero,
            number, numberKilled, numberWounded, numberRouted, killCount, numberReadyToUpgrade));
    }

    public void TroopSideChanged(BattleSideEnum prevSide, BattleSideEnum newSide, IBattleCombatant battleCombatant, BasicCharacterObject character) { }

    public void HeroSkillIncreased(BattleSideEnum side, IBattleCombatant battleCombatant, BasicCharacterObject heroCharacter, SkillObject skill) { }

    public void BattleResultsReady() { }
}
