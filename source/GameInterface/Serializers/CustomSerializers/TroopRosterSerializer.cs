using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class TroopRosterSerializer : ICustomSerializer
    {
        [NonSerialized]
        TroopRoster newRoster;
        [NonSerialized]
        readonly FieldInfo rosterDataField = typeof(TroopRoster).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
        

        readonly TroopRosterElementSerializer[] troops = new TroopRosterElementSerializer[0];
        readonly Guid partyGuid;
        readonly int versionNumber;
        readonly int count;
        readonly bool isPrisonRoster;
#if DEBUG
        // TODO remove debug code
        readonly string partyName;
#endif

        public TroopRosterSerializer(TroopRoster roster)
        {
            versionNumber = roster.VersionNo;
            count = roster.Count;
            isPrisonRoster = roster.IsPrisonRoster;

            TroopRosterElement[] troops = (TroopRosterElement[])rosterDataField.GetValue(roster);

            if(count > 0 && troops != null)
            {
                this.troops = troops.Take(count).AsParallel().Select(troop => new TroopRosterElementSerializer(troop)).ToArray();
            }

            PartyBase partyBase = (PartyBase)typeof(TroopRoster)
                .GetField("<OwnerParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(roster);

            partyName = partyBase.Name.ToString();

            partyGuid = CoopObjectManager.GetGuid(partyBase);
        }

        public object Deserialize()
        {
            newRoster = TroopRoster.CreateDummyTroopRoster();

            newRoster.IsPrisonRoster = isPrisonRoster;

            // Unpack VersionNo
            typeof(TroopRoster)
                .GetField("<VersionNo>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(newRoster, versionNumber);

            // Unpack _count
            typeof(TroopRoster)
                .GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(newRoster, count);

            return newRoster;
        }

        public void ResolveReferenceGuids()
        {
            if (newRoster == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }
            
            // Unpack OwnerParty
            PartyBase partyBase = CoopObjectManager.GetObject<PartyBase>(partyGuid);

            typeof(TroopRoster)
                .GetField("<OwnerParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(newRoster, partyBase);

            // Unpack Troops
            TroopRosterElement[] troops = new TroopRosterElement[0];

            if (count > 0)
            {
                troops = this.troops.Take(count).Select(serializer => serializer.UnpackObject()).ToArray();
            }

            typeof(TroopRoster)
                .GetField("data", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newRoster, troops);

            typeof(TroopRoster)
                .GetField("_troopRosterElements", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newRoster, new List<TroopRosterElement>(troops));
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}