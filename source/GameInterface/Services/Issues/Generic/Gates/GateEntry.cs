using System;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.Gates;

/// <summary>
/// One declarative gate registration: which Hero a given patched method's ownership check should resolve
/// against, and which of the 4 <see cref="GateKind"/> shapes it needs. <typeparamref name="TInstance"/> is
/// whatever the real Harmony patch's own <c>__instance</c> parameter type is (a Quest, an IssueBase, or - for
/// the "gate a whole RegisterEvents-wired listener" shape - a Behavior singleton).
///
/// This does NOT replace the concrete per-site <c>[HarmonyPatch]</c> shell every real gate needs (Harmony
/// patches must be concrete static methods with a fixed signature the patched method dictates - see
/// <see cref="GateAndInjectDescriptor{TQuest}"/>'s own doc comment for the same constraint stated explicitly).
/// It exists so a migrated type's full gate inventory is declared and readable in one place (see
/// <c>QuestTypeDescriptor.Gates</c>), even though each gate's actual Harmony attachment point remains a
/// hand-written shell that calls into <see cref="OwnershipGateRunner"/> instead of re-deriving the ownership
/// check by hand.
/// </summary>
public sealed record GateEntry<TInstance>(
    string MethodName,
    GateKind Kind,
    Func<TInstance, Hero> QuestGiverSelector);

/// <summary>Per-issue-type table of <see cref="GateEntry{TInstance}"/> registrations - see
/// <see cref="GateEntry{TInstance}"/>'s own doc comment for what this does and does not replace.</summary>
public sealed class GenericGates<TInstance>
{
    private readonly System.Collections.Generic.List<GateEntry<TInstance>> _entries = new();

    public System.Collections.Generic.IReadOnlyList<GateEntry<TInstance>> Entries => _entries;

    public GenericGates<TInstance> Add(GateEntry<TInstance> entry)
    {
        _entries.Add(entry);
        return this;
    }
}
