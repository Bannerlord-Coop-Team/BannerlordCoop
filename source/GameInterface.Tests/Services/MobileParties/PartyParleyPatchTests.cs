using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// v1.5.6 parley with a party on the map (Ctrl+Click) would start a local conversation with a party the server owns,
/// so the encounter model refuses it until #4601 sends it to the server (#4585).
/// </summary>
public class PartyParleyPatchTests
{
    [Fact]
    public void Patch_ResolvesItsTarget()
    {
        var harmony = new Harmony(nameof(PartyParleyPatchTests));
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(PartyParleyPatch)).Patch();

            // Harmony returns the replacement method, named after the target with a _PatchN suffix.
            Assert.Contains(patched, method => method.Name.Contains(nameof(DefaultEncounterModel.CanMainHeroDoParleyWithParty)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
