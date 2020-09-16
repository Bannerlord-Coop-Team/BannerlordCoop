using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class MobilePartySerializer : CustomSerializer
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        public MobileParty mobileParty;

        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        Hero hero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        List<string> attachedPartiesNames = new List<string>();
        string stringId;
        
        public MobilePartySerializer(MobileParty mobileParty) : base(mobileParty)
        {
            stringId = mobileParty.StringId;

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(mobileParty);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_currentSettlement":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<LastVisitedSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<Ai>k__BackingField":
                        // PartyAi
                        // NOTE may not be needed due to player control
                        break;
                    case "<Party>k__BackingField":
                        SNNSO.Add(fieldInfo, new PartyBaseSerializer((PartyBase)value));
                        break;
                    case "_disorganizedUntilTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_ignoredUntilTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_initiativeRestoreTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_targetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_moveTargetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_aiPathLastFace":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<Path>k__BackingField":
                        SNNSO.Add(fieldInfo, new NavigationPathSerializer((NavigationPath)value));
                        break;
                    case "_partiesAroundPosition":
                        SNNSO.Add(fieldInfo, new MobilePartiesAroundPositionListSerializer((MobilePartiesAroundPositionList)value));
                        break;
                    case "_nextAiCheckTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<CurrentNavigationFace>k__BackingField":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<AttachedParties>k__BackingField":
                        MBReadOnlyList<MobileParty> attachedParties = (MBReadOnlyList<MobileParty>)value;
                        foreach(MobileParty attachedParty in attachedParties)
                        {
                            attachedPartiesNames.Add(attachedParty.Name.ToString());
                        }
                        break;
                    case "_actualClan":
                        SNNSO.Add(fieldInfo, new ClanSerializer((Clan)value));
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                }
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        /// <summary>
        /// For assigning PlayerHeroSerializer reference for deserialization
        /// </summary>
        /// <param name="hero">PlayerHeroSerializer used by partyBaseSerializer</param>
        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }

        public override object Deserialize()
        {
            MobileParty newMobileParty = MBObjectManager.Instance.CreateObject<MobileParty>(stringId);

            // Circular referenced object needs assignment before deserialize
            if (hero == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                // Pass references to specified serializers
                switch (entry.Value)
                {
                    case PartyBaseSerializer partyBaseSerializer:
                        partyBaseSerializer.SetHeroReference(hero);
                        partyBaseSerializer.SetMobilePartyReference(newMobileParty);
                        entry.Key.SetValue(newMobileParty, partyBaseSerializer.Deserialize(newMobileParty.Party));
                        break;
                    case ClanSerializer clanSerializer:
                        clanSerializer.SetHeroReference(hero);
                        break;
                    default:
                        entry.Key.SetValue(newMobileParty, entry.Value.Deserialize());
                        break;
                }
            }

            return base.Deserialize(newMobileParty);
        }
    }
}
