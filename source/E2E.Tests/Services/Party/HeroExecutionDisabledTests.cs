using GameInterface.Services.Party.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Executing prisoner heroes is disabled in coop
/// (<see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/2310">issue #2310</see>):
/// the kill rode <c>KillCharacterAction</c> and its death/inheritance follow-ups, which crashed the game
/// when it targeted a lord or player. <see cref="PartyScreenLogicPatches"/> now skips
/// <see cref="PartyScreenLogic.ExecuteTroop"/> wholesale, forces <see cref="PartyScreenLogic.IsExecutable"/>
/// to false (disabling the party screen's execute button and failing command validation), and replaces the
/// button's tooltip with a coop-specific reason.
/// </summary>
public class HeroExecutionDisabledTests
{
    [Fact]
    public void IsExecutablePrefix_ForcesFalse_AndSkipsOriginal()
    {
        bool result = true;

        bool runOriginal = PartyScreenLogicPatches.IsExecutablePrefix(ref result);

        Assert.False(runOriginal);
        Assert.False(result);
    }

    [Fact]
    public void ExecuteTroopPrefix_SkipsOriginal()
    {
        Assert.False(PartyScreenLogicPatches.ExecuteTroopPrefix());
    }

    [Fact]
    public void GetExecutableReasonStringPrefix_ReplacesNativeReason_AndSkipsOriginal()
    {
        string result = "Cannot execute hero right now";

        bool runOriginal = PartyScreenLogicPatches.GetExecutableReasonStringPrefix(ref result);

        Assert.False(runOriginal);
        Assert.Equal(PartyScreenLogicPatches.ExecutionDisabledReason, result);
    }

    /// <summary>
    /// Applies the patch class exactly as PatchAll does, then drives the patched natives on an
    /// uninitialized <see cref="PartyScreenLogic"/>. Without the patches every call below would throw
    /// (null command, null character, no Campaign), so passing proves the natives are short-circuited
    /// before they touch game state.
    /// </summary>
    [Fact]
    public void PatchedPartyScreenLogic_ShortCircuitsExecution()
    {
        var harmony = new Harmony("e2e.heroexecution.patchtest");
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(PartyScreenLogicPatches)).Patch()
                          ?? new List<MethodInfo>();

            Assert.Contains(patched, m => m.Name.Contains(nameof(PartyScreenLogic.ExecuteTroop)));
            Assert.Contains(patched, m => m.Name.Contains(nameof(PartyScreenLogic.IsExecutable)));
            Assert.Contains(patched, m => m.Name.Contains(nameof(PartyScreenLogic.GetExecutableReasonString)));

            var logic = (PartyScreenLogic)FormatterServices.GetUninitializedObject(typeof(PartyScreenLogic));

            Assert.False(logic.IsExecutable(PartyScreenLogic.TroopType.Prisoner, null, PartyScreenLogic.PartyRosterSide.Right));
            Assert.Equal(PartyScreenLogicPatches.ExecutionDisabledReason, logic.GetExecutableReasonString(null, false));

            logic.ExecuteTroop(null);
        }
        finally
        {
            harmony.UnpatchAll("e2e.heroexecution.patchtest");
        }
    }
}
