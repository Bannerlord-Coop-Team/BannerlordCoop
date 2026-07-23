using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests for the player-party protection in <see cref="DestroyPartyActionPatch"/>. The guard is
/// checked before the skip-patches (AllowedThread) guard, so a destroy that runs nested inside
/// another action's scope cannot bypass it.
/// </summary>
public class DestroyPartyActionPatchTests
{
    [Fact]
    public void PrefixApply_PlayerParty_BlockedEvenUnderAllowedThread()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        // The guard logs the party name; give the uninitialized party a PartyBase so the Name
        // getter does not dereference null.
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party))
            .SetValue(party, ObjectHelper.SkipConstructor<PartyBase>());

        var playerObjects = (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
        playerObjects.Add(party, new ControlledObjectInfo("TestPlayer", null!));

        try
        {
            bool runOriginal;
            using (new AllowedThread())
            {
                runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
            }

            Assert.False(runOriginal);
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }

    [Fact]
    public void PrefixApply_NonPlayerParty_UnderAllowedThread_RunsOriginal()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();

        bool runOriginal;
        using (new AllowedThread())
        {
            runOriginal = DestroyPartyActionPatch.PrefixApply(null, party);
        }

        Assert.True(runOriginal);
    }
}
