using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.DebugUtil
{
    public static class PartySpawnHelper
    {
        public static MobileParty SpawnTestersNearby(Vec2 position, float spawnRadius)
        {
            // We need to assign an owner to the party, otherwise the Bannerlord main loop runs into a segfault.
            // We'll just pick the owner of a random nearby settlement.
            Settlement s = Settlement.FindSettlementsAroundPosition(position, 100).First();
            return MobileParty.CreateParty("coop_testers", null, delegate(MobileParty party)
            {
                party.Aggressiveness = 0;
                party.IsActive = true;
                party.IsVisible = true;
                party.Party.Visuals.SetMapIconAsDirty();
                party.SetCustomName(new TextObject("Testers"));                

                CharacterObject obj = Campaign.Current.ObjectManager.GetObject<CharacterObject>("gear_practice_dummy_empire");
                TroopRoster roster = new TroopRoster(party.Party);
                roster.AddToCounts(obj, 5 - roster.TotalManCount);
                party.InitializeMobilePartyAroundPosition(
                    roster,
                    new TroopRoster(party.Party),
                    position,
                    spawnRadius,
                    spawnRadius);
                party.Ai.DisableAi();
                party.SetPartyUsedByQuest(true);
                party.Party.SetCustomOwner(s.Owner);
            });
        }
    }
}
