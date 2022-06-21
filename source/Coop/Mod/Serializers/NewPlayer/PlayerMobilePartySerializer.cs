using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerMobilePartySerializer : CustomSerializer
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
        [NonSerialized]
        Clan clan;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        List<string> attachedPartiesNames = new List<string>();
        string stringId;
        

        public PlayerMobilePartySerializer(MobileParty mobileParty) : base(mobileParty)
        {
            stringId = mobileParty.StringId;

            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

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
                    case "<Id>k__BackingField":
                        // Ignore current MB id
                        break;
                    case "_currentSettlement":
                        SNNSO.Add(fieldInfo, new PlayerSettlementSerializer((Settlement)value));
                        break;
                    case "<LastVisitedSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new PlayerSettlementSerializer((Settlement)value));
                        break;
                    case "<Ai>k__BackingField":
                        // PartyAi
                        // NOTE may not be needed due to player control
                        break;
                    case "<Party>k__BackingField":
                        SNNSO.Add(fieldInfo, new PlayerPartyBaseSerializer((PartyBase)value));
                        break;
                    case "_disorganizedUntilTime":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_ignoredUntilTime":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_initiativeRestoreTime":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_targetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new Custom.PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_moveTargetAiFaceIndex":
                        SNNSO.Add(fieldInfo, new Custom.PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "_aiPathLastFace":
                        SNNSO.Add(fieldInfo, new Custom.PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<Path>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.NavigationPathSerializer((NavigationPath)value));
                        break;
                    case "_partiesAroundPosition":
                        SNNSO.Add(fieldInfo, new PlayerMobilePartiesAroundPositionListSerializer((MobilePartiesAroundPositionList)value));
                        break;
                    case "_nextAiCheckTime":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<CurrentNavigationFace>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.PathFaceRecordSerializer((PathFaceRecord)value));
                        break;
                    case "<AttachedParties>k__BackingField":
                        MBReadOnlyList<MobileParty> attachedParties = (MBReadOnlyList<MobileParty>)value;
                        foreach(MobileParty attachedParty in attachedParties)
                        {
                            attachedPartiesNames.Add(attachedParty.Name.ToString());
                        }
                        break;
                    case "_actualClan":
                        break;
                    case "<StationaryStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_partyComponent":
                        SNNSO.Add(fieldInfo, new PlayerLordPartyComponentSerializer((LordPartyComponent)value));
                        break;
                    case "_pureSpeedExplainer":
                        // TODO Joke Fix this
                        break;
                    case "<TaleWorlds.CampaignSystem.Map.ILocatable<TaleWorlds.CampaignSystem.Party.MobileParty>.NextLocatable>k__BackingField":
                        // TODO Joke Fix this
                        //Suggestion: Remove FieldInfos, that has CachedData attribute from NonSerializableObjects list.
                        //So we won't have these empty cases.
                        break;
                    case "_targetSettlement":
                        // TODO Joke Fix this
                        break;
                    case "<AiBehaviorObject>k__BackingField":
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;
                }
            }

            // TODO manage collections

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }
        }

        /// <summary>
        /// For assigning PlayerHeroSerializer reference for deserialization
        /// </summary>
        /// <param name="hero">PlayerHeroSerializer used by partyBaseSerializer</param>
        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }

        /// <summary>
        /// For assigning PlayerClanSerializer reference for deserialization
        /// </summary>
        /// <param name="hero">PlayerClanSerializer used by partyBaseSerializer</param>
        public void SetClanReference(Clan clan)
        {
            this.clan = clan;
        }

        public override object Deserialize()
        {
            MobileParty newMobileParty = MBObjectManager.Instance.CreateObject<MobileParty>(stringId);

            MethodInfo _AddMobileParty = typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.NonPublic | BindingFlags.Instance);

            _AddMobileParty.Invoke(Campaign.Current.CampaignObjectManager, new object[] { newMobileParty });

            // Circular referenced object needs assignment before deserialize
            if (hero == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            // Circular referenced object needs assignment before deserialize
            if (clan == null)
            {
                throw new SerializationException("Must set clan reference before deserializing. Use SetClanReference()");
            }

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                // Pass references to specified serializers
                switch (entry.Value)
                {
                    case PlayerPartyBaseSerializer partyBaseSerializer:
                        partyBaseSerializer.SetHeroReference(hero);
                        partyBaseSerializer.SetMobilePartyReference(newMobileParty);
                        entry.Key.SetValue(newMobileParty, partyBaseSerializer.Deserialize(newMobileParty.Party));
                        break;
                    case PlayerLordPartyComponentSerializer lordPartyComponentSerializer:
                        lordPartyComponentSerializer.SetHeroReference(hero);
                        entry.Key.SetValue(newMobileParty, lordPartyComponentSerializer.Deserialize());
                        break;
                    default:
                        entry.Key.SetValue(newMobileParty, entry.Value.Deserialize());
                        break;
                }
            }


            typeof(MobileParty).GetField("_actualClan", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(newMobileParty, clan);

            typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Campaign.Current.CampaignObjectManager, new object[] { newMobileParty });

            newMobileParty = (MobileParty)base.Deserialize(newMobileParty);

            return newMobileParty;
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}
