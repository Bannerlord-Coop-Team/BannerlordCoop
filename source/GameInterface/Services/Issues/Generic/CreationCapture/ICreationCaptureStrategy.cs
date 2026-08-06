namespace GameInterface.Services.Issues.Generic.CreationCapture;

/// <summary>
/// Generic-infrastructure primitive for the "creation-capture" shape: one-shot, roll once, capture once,
/// broadcast once, done for the object's lifetime. A single non-deterministic creation-time roll is captured on
/// the genuine creator and force-written by reflection onto every other peer's own independently constructed
/// instance.
///
/// TIssue is the concrete <see cref="TaleWorlds.CampaignSystem.Issues.IssueBase"/> subtype; TFields is whatever
/// tuple/record of rolled values this issue type's constructor needs frozen across peers (a single reference for
/// a one-field case; a wider shape for a type with more than one creation-time roll).
/// </summary>
public interface ICreationCaptureStrategy<TIssue, TFields>
{
    /// <summary>Reads the already-rolled fields off a just-constructed, genuine (server-side) issue instance.
    /// Returns false if the issue has no rolled fields yet, which should never happen for a real instance.</summary>
    bool TryCaptureFields(TIssue issue, out TFields fields);

    /// <summary>Builds a byte-identical <typeparamref name="TIssue"/> for a non-creating peer: constructs it the
    /// normal way (which independently re-rolls its own <typeparamref name="TFields"/>), then force-overwrites
    /// the rolled field(s) to the authoritative, captured values.</summary>
    TIssue ConstructReplicated(TaleWorlds.CampaignSystem.Hero owner, TFields fields);
}
