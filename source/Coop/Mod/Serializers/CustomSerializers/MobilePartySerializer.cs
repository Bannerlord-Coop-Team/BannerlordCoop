using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class MobilePartySerializer : CustomSerializerWithGuid
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        public MobileParty mobileParty;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        List<Guid> attachedParties = new List<Guid>();
        Guid currentSettlement;
        Guid lastVistedSettlement;
        Guid partyBase;
        Guid clan;
        Guid targetSettlement;

        public MobilePartySerializer(MobileParty mobileParty) : base(mobileParty)
        {
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
                        currentSettlement = CoopObjectManager.GetGuid(value);
                        break;
                    case "<LastVisitedSettlement>k__BackingField":
                        lastVistedSettlement = CoopObjectManager.GetGuid(value);
                        break;
                    case "<Ai>k__BackingField":
                        // PartyAi
                        // NOTE may not be needed due to player control
                        break;
                    case "<Party>k__BackingField":
                        partyBase = CoopObjectManager.GetGuid(value);
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
                            this.attachedParties.Add(CoopObjectManager.GetGuid(attachedParty));
                        }
                        break;
                    case "_actualClan":
                        clan = CoopObjectManager.GetGuid(value);
                        break;
                    case "<StationaryStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_partyComponent":
                        SNNSO.Add(fieldInfo, new PartyComponentSerializer((PartyComponent)value));
                        break;
                    case "_pureSpeedExplainer":
                        // TODO Joke Fix this
                        break;
                    case "<TaleWorlds.CampaignSystem.ILocatable<TaleWorlds.CampaignSystem.MobileParty>.NextLocatable>k__BackingField":
                        // TODO Joke Fix this
                        break;
                    case "_targetSettlement":
                        targetSettlement = CoopObjectManager.GetGuid(value);
                        break;
                    case "<AiBehaviorObject>k__BackingField":
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

        public override object Deserialize()
        {
            mobileParty = new MobileParty();

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(mobileParty, entry.Value.Deserialize());
            }

            return base.Deserialize(mobileParty);
        }

        public override void ResolveReferenceGuids()
        {
            if (mobileParty == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            List<MobileParty> attachedParties = this.attachedParties
                .Select(x => CoopObjectManager.GetObject<MobileParty>(x)).ToList();

            mobileParty.GetType()
                .GetField("<AttachedParties>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, new MBReadOnlyList<MobileParty>(attachedParties));

            mobileParty.GetType()
                .GetField("_currentSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, CoopObjectManager.GetObject(currentSettlement));

            mobileParty.GetType()
                .GetField("<LastVisitedSettlement>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, CoopObjectManager.GetObject(lastVistedSettlement));

            mobileParty.GetType()
                .GetField("<Party>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, CoopObjectManager.GetObject(partyBase));

            mobileParty.GetType()
                .GetField("_actualClan", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, CoopObjectManager.GetObject(clan));

            mobileParty.GetType()
                .GetField("_targetSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mobileParty, CoopObjectManager.GetObject(targetSettlement));

            mobileParty.MemberRoster.OnHeroHealthStatusChanged(mobileParty.LeaderHero);
        }
    }
}
