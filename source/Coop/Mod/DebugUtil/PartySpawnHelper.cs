using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.DebugUtil
{
    public static class PartySpawnHelper
    {
        private static readonly int TesterCount = 0;

        public static MobileParty SpawnTestersNear(MobileParty nearbyParty)
        {
            // Init party
            MobileParty party =
                MBObjectManager.Instance.CreateObject<MobileParty>(
                    "coop_mod_testers_" + TesterCount);
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            CharacterObject obj =
                Campaign.Current.ObjectManager.GetObject<CharacterObject>(
                    "tutorial_placeholder_volunteer");
            roster.AddToCounts(obj, 5 - roster.TotalManCount);
            TroopRoster prisonerRoster = TroopRoster.CreateDummyTroopRoster();
            party.InitializeMobileParty(
                roster,
                prisonerRoster,
                nearbyParty.Position2D,
                5f,
                2f);
            party.SetCustomName(new TextObject("testers"));
            //party.Party.Owner = null;
            party.Party.Visuals.SetMapIconAsDirty();

            return party;
        }
    }
}
