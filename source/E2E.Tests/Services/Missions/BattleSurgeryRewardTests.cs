using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleSurgeryRewardTests : MissionTestEnvironment
{
    public BattleSurgeryRewardTests(ITestOutputHelper output) : base(output)
    {
        // The headless environment omits join-time registration of pre-existing skills.
        uint medicineHandle = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.AddExisting("SkillObject_medicine", DefaultSkills.Medicine));
            Assert.True(Server.ObjectManager.TryGetHandle(DefaultSkills.Medicine, out medicineHandle));
        });
        foreach (var client in Clients)
            client.Call(() => Assert.True(client.ObjectManager.AddExisting(
                "SkillObject_medicine", DefaultSkills.Medicine, medicineHandle)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClientSurgeryReward_IncreasesServerSurgeonMedicineAndReplicates(bool surgerySuccess)
    {
        var (mapEventId, partyIds) = SetupCoopBattle("attacker", "defender");
        var surgeonId = CreateRegisteredObject<Hero>();
        float before = 0;
        float expectedGain = 0;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            var surgeon = Server.GetRegisteredObject<Hero>(surgeonId);
            party.MemberRoster.AddToCounts(surgeon.CharacterObject, 1);
            party.SetPartySurgeon(surgeon);
            surgeon.HeroDeveloper.SetFocus(DefaultSkills.Medicine, 5);
            before = surgeon.HeroDeveloper.GetSkillXp(DefaultSkills.Medicine);
            expectedGain = (surgerySuccess ? 30f : 15f)
                * Campaign.Current.Models.GenericXpModel.GetXpMultiplier(surgeon)
                * surgeon.HeroDeveloper.GetFocusFactor(DefaultSkills.Medicine);
        });

        var client = Clients.First();
        client.Call(() =>
        {
            BattleSpawnGate.BeginBattle(mapEventId);
            try
            {
                SkillLevelingManager.OnSurgeryApplied(
                    client.GetRegisteredObject<MobileParty>(partyIds[0]), surgerySuccess, 3);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
        });

        float after = 0;
        Server.Call(() => after = Server.GetRegisteredObject<Hero>(surgeonId).HeroDeveloper.GetSkillXp(DefaultSkills.Medicine));
        Assert.True(expectedGain > 0);
        Assert.Equal(before + expectedGain, after);
        foreach (var observer in Clients)
            observer.Call(() => Assert.Equal(after, observer.GetRegisteredObject<Hero>(surgeonId).HeroDeveloper.GetSkillXp(DefaultSkills.Medicine)));
        var reward = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBattleSurgeryReward>());
        Assert.Equal(partyIds[0], reward.PartyId);
        Assert.Equal(mapEventId, reward.MapEventId);
        Assert.Equal(surgerySuccess, reward.SurgerySuccess);
        Assert.Equal(3, reward.TroopTier);
    }

    [Fact]
    public void RewardFromDifferentMapEvent_DoesNotIncreaseMedicine()
    {
        var (_, partyIds) = SetupCoopBattle("attacker", "defender");
        var (otherMapEventId, _) = SetupCoopBattle("other-attacker", "other-defender");
        var surgeonId = CreateRegisteredObject<Hero>();
        float before = 0;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            var surgeon = Server.GetRegisteredObject<Hero>(surgeonId);
            party.MemberRoster.AddToCounts(surgeon.CharacterObject, 1);
            party.SetPartySurgeon(surgeon);
            before = surgeon.HeroDeveloper.GetSkillXp(DefaultSkills.Medicine);
        });
        Clients.First().Call(() => Clients.First().Resolve<INetwork>().SendAll(
            new NetworkBattleSurgeryReward(partyIds[0], otherMapEventId, true, 3)));
        Server.Call(() => Assert.Equal(before, Server.GetRegisteredObject<Hero>(surgeonId).HeroDeveloper.GetSkillXp(DefaultSkills.Medicine)));
    }
}
