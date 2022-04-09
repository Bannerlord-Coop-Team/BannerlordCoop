using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.DebugUtil
{
    public static class PartySpawnHelper
    {
        public static MobileParty SpawnTestersNear(MobileParty nearbyParty, float spawnRadius)
        {
            return MobileParty.CreateParty("coop_testers", null, delegate(MobileParty party)
            {
                party.Aggressiveness = 0;
                party.IsActive = true;
                party.IsVisible = true;
                party.Party.Visuals.SetMapIconAsDirty();
                party.SetCustomName(new TextObject("Testers"));

                CharacterObject obj = Campaign.Current.ObjectManager.GetObject<CharacterObject>("tutorial_placeholder_volunteer");
                TroopRoster roster = new TroopRoster(party.Party);
                roster.AddToCounts(obj, 5 - roster.TotalManCount);
                party.InitializeMobilePartyAroundPosition(
                    roster,
                    new TroopRoster(party.Party),
                    nearbyParty.Position2D,
                    spawnRadius,
                    spawnRadius);
            });
        }
    }
}
