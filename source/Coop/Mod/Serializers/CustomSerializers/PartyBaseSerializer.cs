using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PartyBaseSerializer : CustomSerializer
    {
        // TODO add setters and use in parent, see characterobjectserializer
        [NonSerialized]
        MobilePartySerializer mobilePartySerializer;
        // TODO add setters and use in parent, see characterobjectserializer
        [NonSerialized]
        PlayerHeroSerializer heroSerializer;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public PartyBaseSerializer(PartyBase partyBase) : base(partyBase)
        {
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
                        // Not needed, populated at deserialize
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
                        // Not needed, populated at deserialize
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }

            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        public void SetHeroReference(PlayerHeroSerializer playerHeroSerializer)
        {
            heroSerializer = playerHeroSerializer;
        }

        public void SetMobilePartyReference(MobilePartySerializer mobilePartySerializer)
        {
            this.mobilePartySerializer = mobilePartySerializer;
        }

        public override object Deserialize()
        {
            if (heroSerializer == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }
            else if(mobilePartySerializer == null)
            {
                throw new SerializationException("Must set mobileParty reference before deserializing. Use SetMobilePartyReference()");
            }

            PartyBase newPartyBase = new PartyBase(mobilePartySerializer.mobileParty);

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newPartyBase, entry.Value.Deserialize());
            }

            newPartyBase.GetType().GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newPartyBase, heroSerializer.hero);
            newPartyBase.GetType().GetField("<MobileParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newPartyBase, mobilePartySerializer.mobileParty);

            return base.Deserialize(newPartyBase);
        }
    }
}