using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerPartyBaseSerializer : CustomSerializer
    {
        [NonSerialized]
        MobileParty mobileParty;
        [NonSerialized]
        Hero hero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public PlayerPartyBaseSerializer(PartyBase partyBase) : base(partyBase)
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
                        //SNNSO.Add(fieldInfo, new Custom.TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<PrisonRoster>k__BackingField":
                        // TroopRoster
                        //SNNSO.Add(fieldInfo, new Custom.TroopRosterSerializer((TroopRoster)value));
                        break;
                    case "<ItemRoster>k__BackingField":
                        // ItemRoster
                        SNNSO.Add(fieldInfo, new Custom.ItemRosterSerializer((ItemRoster)value));
                        break;
                    case "Random":
                        // DeterministicRandom
                        SNNSO.Add(fieldInfo, new Custom.DeterministicRandomSerializer((DeterministicRandom)value));
                        break;
                    case "_lastEatingTime":
                        //CampaignTime
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_owner":
                        // Not needed, populated at deserialize
                        break;
                    case "_leader":
                        // Not needed, populated at deserialize
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }

            FieldInfo indexField = partyBase.GetType().GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic);
            SerializableObjects.Remove(indexField);
        }

        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }

        public void SetMobilePartyReference(MobileParty mobileParty)
        {
            this.mobileParty = mobileParty;
        }

        public object Deserialize(PartyBase newPartyBase)
        {
            if (hero == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }
            else if(mobileParty == null)
            {
                throw new SerializationException("Must set mobileParty reference before deserializing. Use SetMobilePartyReference()");
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newPartyBase, entry.Value.Deserialize());
            }

            newPartyBase.AddElementToMemberRoster(hero.CharacterObject, 1, true);
            newPartyBase.GetType().GetField("<MobileParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newPartyBase, mobileParty);

            IPartyVisual newVisual = Campaign.Current.VisualCreator.PartyVisualCreator.CreatePartyVisual();
            newPartyBase.GetType().GetField("_visual", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(newPartyBase, newVisual);

            return base.Deserialize(newPartyBase);
        }

        public override object Deserialize()
        {
            throw new NotImplementedException();
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}