namespace GameInterface.Services.Issues.Generic.PartySpawn;

/// <summary>
/// Names the Harmony attachment point a given quest type's party-spawn gate should use - NOT a
/// reconciliation-logic taxonomy. Do not read these cases as an exhaustive list of what has to be hand-written
/// per instance: they fix only the attachment point; matching each instance's own reconciliation shape (which
/// hooks fire, what state gets captured) is separate, per-type work every time.
/// </summary>
public abstract record PartySpawnTrigger
{
    /// <summary>The dominant real shape - a Harmony Prefix+Postfix pair applied DIRECTLY to the vanilla spawn
    /// method itself. Prefix redirects a client to a network request and skips the real body; Postfix broadcasts
    /// after the real body ran on server/host-owner. Real instances: EscortMerchantCaravan, Smugglers,
    /// LandLordCompanyOfTrouble, RivalGangMovingIn.</summary>
    public sealed record SpawnMethodWrap(string MethodName) : PartySpawnTrigger;

    /// <summary>MerchantArmyOfPoachers' shape: a Postfix on the named enclosing dialogue Consequence, calling a
    /// REIMPLEMENTED spawn (vanilla's own spawn call site is dead/unreachable once this fires).</summary>
    public sealed record DialogueConsequence(string ConsequenceMethodName) : PartySpawnTrigger;

    /// <summary>A <c>RegisterEvents()</c>-wired <c>CampaignEvents</c> listener whose OWN vanilla gate is
    /// satisfiable by any peer (not just the accepter), unlike the usual "inert on non-owner mirrors" case. Needs
    /// an ownership pre-gate on the WHOLE listener, not just the eventual spawn call. Real instance:
    /// GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch (<c>OnSettlementEnter</c>).</summary>
    public sealed record EventListener(string ListenerMethodName) : PartySpawnTrigger;
}
