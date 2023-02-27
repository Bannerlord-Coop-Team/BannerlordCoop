using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Patches
{
    public class PartyMovementPatchTests
    {
        [Fact]
        public void SetPartyTargetPosition_OverrideTest()
        {
            Harmony harmony = new Harmony("TestHarmony");

            harmony.PatchAll(typeof(PartyMovementPatch).Assembly);

            MobileParty party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            MobileParty party2 = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            MobileParty party3 = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            Vec2 startPos = new Vec2(1, 1);
            Vec2 endPos = new Vec2(2, 2);

            PartyMovementPatch.MobileParty_TargetPosition.SetValue(party, startPos);

            Assert.Equal(startPos, party.TargetPosition);

            MobilePartyInterface.ControlledParties.Add(party);
            MobilePartyInterface.ControlledParties.Add(party2);
            MobilePartyInterface.ControlledParties.Add(party3);

            PartyMovementPatch.MobileParty_TargetPosition.SetValue(party, endPos);

            Assert.Equal(startPos, party.TargetPosition);

            PartyMovementPatch.SetTargetPositionOverride(party, ref endPos);

            Assert.Equal(endPos, party.TargetPosition);
        }
    }
}
