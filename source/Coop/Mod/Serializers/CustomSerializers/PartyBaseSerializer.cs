using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PartyBaseSerializer : CustomSerializer
    {
        MobilePartySerializer mobilePartySerializer;
        PlayerHeroSerializer hero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        public PartyBaseSerializer(PartyBase partyBase, MobilePartySerializer mobilePartySerializer, PlayerHeroSerializer hero) : base(partyBase)
        {
            this.mobilePartySerializer = mobilePartySerializer;
            this.hero = hero;

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                object value = fieldInfo.GetValue(partyBase);

                if (value == null)
                {
                    continue;
                }

                switch (fieldInfo.Name)
                {
                    case "_visual":
                        // PartyVisual
                        // Generate on server
                        break;
                    case "<MobileParty>k__BackingField":
                        // MobileParty
                        this.mobilePartySerializer = mobilePartySerializer;
                        break;
                    case "<MemberRoster>k__BackingField":
                        // TroopRoster
                        SNNSO.Add(fieldInfo, new TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<PrisonRoster>k__BackingField":
                        // TroopRoster
                        SNNSO.Add(fieldInfo, new TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<ItemRoster>k__BackingField":
                        // ItemRoster
                        SNNSO.Add(fieldInfo, new ItemRosterSerializer((ItemRoster)value));
                        break;
                    case "Random":
                        // DeterministicRandom
                        SNNSO.Add(fieldInfo, new DeterministicRandomSerializer((DeterministicRandom)value));
                        break;
                    case "_owner":
                        this.hero = hero;
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }
        }

        public override object Deserialize()
        {
            return new PartyBase(mobilePartySerializer.mobileParty);
        }
    }
}