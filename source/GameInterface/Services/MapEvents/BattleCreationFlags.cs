namespace GameInterface.Services.MapEvents;

/// <summary>
/// Snapshot of the <see cref="TaleWorlds.CampaignSystem.Encounters.PlayerEncounter"/> "force" flags that
/// determine which kind of <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>
/// <c>PlayerEncounter.StartBattleInternal</c> would create.
/// </summary>
/// <remarks>
/// These are captured on the client (which cannot create the authoritative MapEvent itself) and sent to
/// the server so it can pick the exact same battle branch when creating the MapEvent on the client's behalf.
/// The remaining branch conditions in <c>StartBattleInternal</c> depend only on party/settlement state, which
/// the server re-evaluates directly from the resolved parties (see <see cref="MapEventBattleFactory"/>).
/// </remarks>
internal readonly struct BattleCreationFlags
{
    public readonly bool ForceRaid;
    public readonly bool ForceSallyOut;
    public readonly bool ForceVolunteers;
    public readonly bool ForceSupplies;
    public readonly bool IsSallyOutAmbush;
    public readonly bool ForceBlockadeAttack;
    public readonly bool ForceBlockadeSallyOutAttack;
    public readonly bool ForceHideoutSendTroops;

    public BattleCreationFlags(
        bool forceRaid,
        bool forceSallyOut,
        bool forceVolunteers,
        bool forceSupplies,
        bool isSallyOutAmbush,
        bool forceBlockadeAttack,
        bool forceBlockadeSallyOutAttack,
        bool forceHideoutSendTroops)
    {
        ForceRaid = forceRaid;
        ForceSallyOut = forceSallyOut;
        ForceVolunteers = forceVolunteers;
        ForceSupplies = forceSupplies;
        IsSallyOutAmbush = isSallyOutAmbush;
        ForceBlockadeAttack = forceBlockadeAttack;
        ForceBlockadeSallyOutAttack = forceBlockadeSallyOutAttack;
        ForceHideoutSendTroops = forceHideoutSendTroops;
    }
}
