using GameInterface.Services.MapEvents.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents
{
    /// <summary>
    /// Tests the sortie exemption in <see cref="EncounterManagerPatches"/>.
    ///
    /// SallyOutsCampaignBehavior.CheckSallyOut starts a sortie with
    /// EncounterManager.StartPartyEncounter(settlement.Town.GarrisonParty.Party, BesiegerCamp.LeaderParty.Party).
    /// The server prefix on that method routes any encounter with exactly one player party into a
    /// conversation request, which for a player-led siege swallowed every sortie: vanilla never ran,
    /// StartBattleAction.Apply never ran, and no sally-out map event was ever created, so the AI garrison
    /// could never sally out. Verified live - before the exemption the host logged
    /// "exit=DivertedToConversation" on every 4-hour check with the besieger's map event staying null;
    /// after it, "exit=VanillaRuns" and the besieger gained a SallyOut map event.
    ///
    /// The risk of the exemption is being too broad and letting ordinary AI-vs-player encounters skip the
    /// conversation flow, so the negative cases matter as much as the positive one.
    ///
    /// TaleWorlds.CampaignSystem is publicized for this assembly, so backing fields are set directly.
    /// </summary>
    public class GarrisonSortieGuardTests
    {
        private static readonly MethodInfo IsGarrisonSortie =
            typeof(EncounterManagerPatches).GetMethod("IsGarrisonSortie", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool Invoke(PartyBase attacker, PartyBase defender)
            => (bool)IsGarrisonSortie.Invoke(null, new object?[] { attacker, defender })!;

        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static void SetBacking<T>(object target, string property, T value)
            => SetField(target, $"<{property}>k__BackingField", value);

        // _besiegerCamp and BesiegedSettlement are readonly, so they go through reflection rather than a
        // direct assignment. The search walks base types too: GarrisonPartyComponent inherits
        // <MobileParty>k__BackingField from PartyComponent, and GetField does not see private base fields.
        private static void SetField<T>(object target, string field, T value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }

            Assert.Fail($"field '{field}' not found on {target.GetType().Name} or its base types");
        }

        /// <summary>A mobile party wrapped in a PartyBase, optionally flagged as a garrison.</summary>
        private static (MobileParty party, PartyBase partyBase) CreateParty(bool isGarrison)
        {
            var party = Raw<MobileParty>();
            SetBacking(party, "IsGarrison", isGarrison);

            var partyBase = Raw<PartyBase>();
            SetBacking(partyBase, "MobileParty", party);
            return (party, partyBase);
        }

        /// <summary>A settlement whose Town.GarrisonParty resolves to <paramref name="garrison"/>.</summary>
        private static Settlement CreateBesiegedSettlement(MobileParty garrison)
        {
            var component = Raw<GarrisonPartyComponent>();
            SetBacking(component, "MobileParty", garrison);

            var town = Raw<Town>();
            town.GarrisonPartyComponent = component;

            var settlement = Raw<Settlement>();
            settlement.Town = town;
            return settlement;
        }

        /// <summary>Points a besieging party's camp at <paramref name="besieged"/>.</summary>
        private static void BesiegeWith(MobileParty besieger, Settlement besieged)
        {
            var siegeEvent = Raw<SiegeEvent>();
            SetField(siegeEvent, "BesiegedSettlement", besieged);

            var camp = Raw<BesiegerCamp>();
            SetBacking(camp, "SiegeEvent", siegeEvent);

            SetField(besieger, "_besiegerCamp", camp);
        }

        [Fact]
        public void GarrisonAttackingItsOwnBesieger_IsASortie()
        {
            var (garrison, garrisonBase) = CreateParty(isGarrison: true);
            var (besieger, besiegerBase) = CreateParty(isGarrison: false);
            BesiegeWith(besieger, CreateBesiegedSettlement(garrison));

            Assert.True(Invoke(garrisonBase, besiegerBase));
        }

        [Fact]
        public void GarrisonOfADifferentSettlement_IsNotASortie()
        {
            var (garrison, garrisonBase) = CreateParty(isGarrison: true);
            var (otherGarrison, _) = CreateParty(isGarrison: true);
            var (besieger, besiegerBase) = CreateParty(isGarrison: false);

            // The camp is besieging somewhere else, so this garrison is not the one sallying out.
            BesiegeWith(besieger, CreateBesiegedSettlement(otherGarrison));

            Assert.False(Invoke(garrisonBase, besiegerBase));
        }

        [Fact]
        public void NonGarrisonAttacker_IsNotASortie()
        {
            var (lordParty, lordBase) = CreateParty(isGarrison: false);
            var (besieger, besiegerBase) = CreateParty(isGarrison: false);
            BesiegeWith(besieger, CreateBesiegedSettlement(lordParty));

            Assert.False(Invoke(lordBase, besiegerBase));
        }

        [Fact]
        public void DefenderNotBesieging_IsNotASortie()
        {
            var (_, garrisonBase) = CreateParty(isGarrison: true);
            var (_, wandererBase) = CreateParty(isGarrison: false);

            // No BesiegerCamp at all - an ordinary encounter, which must still reach the conversation flow.
            Assert.False(Invoke(garrisonBase, wandererBase));
        }

        [Fact]
        public void NullParties_AreNotASortie()
        {
            var (_, garrisonBase) = CreateParty(isGarrison: true);

            Assert.False(Invoke(null!, garrisonBase));
            Assert.False(Invoke(garrisonBase, null!));
            Assert.False(Invoke(null!, null!));
        }
    }
}
