using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    class ClanSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Clan newClan;

        List<Guid> Supporters = new List<Guid>();
        List<Guid> Companions = new List<Guid>(); 
        List<Guid> CommanderHeroes = new List<Guid>(); //Does it refer to the lordscache or the heroescache in Bannerlord code? Which is missing from the switch case?

        

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        public ClanSerializer(Clan clan) : base(clan)
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(clan);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "<Name>k__BackingField":
                    case "<InformalName>k__BackingField":
                    case "<EncyclopediaText>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "<Culture>k__BackingField":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    case "<LastFactionChangeTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<SupporterNotables>k__BackingField":
                        foreach (Guid supporters in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            Supporters.Add(supporters);
                        }
                        break;
                    case "<Companions>k__BackingField":
                        foreach (Guid companions in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            Companions.Add(companions);
                        }
                        break;
                    case "<CommanderHeroes>k__BackingField":
                        foreach (Guid commanderheroes in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            CommanderHeroes.Add(commanderheroes);
                        }
                        break;
                    case "_basicTroop":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    case "_leader":
                        // Assigned by SetHeroReference on deserialization
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    case "_banner":
                        SNNSO.Add(fieldInfo, new BannerSerializer((Banner)value));
                        break;
                    case "_home":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    case "<NotAttackableByPlayerUntilTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_defaultPartyTemplate":
                        SNNSO.Add(fieldInfo, new DefaultPartyTemplateSerializer((PartyTemplateObject)value));
                        break;
                    case "_kingdom":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    case "OnPartiesAndLordsCacheUpdated":
                        // Event for clans, add for all clans on kingdom deserialize
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }            
        }

        public override object Deserialize()
        {

            newClan = MBObjectManager.Instance.CreateObject<Clan>();

            // Circular referenced objects
            newClan.GetType().GetField("_leader", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(newClan, null);

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newClan, entry.Value.Deserialize());
            }
            
            return base.Deserialize(newClan);
        }

        public override void ResolveReferenceGuids()
        {
            if (newClan == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }
            //Deserialize the lists
            List<Hero> lCompanions= new List<Hero>();
            List<Hero> lSupporters = new List<Hero>();
            List<Hero> lCommanderHeroes = new List<Hero>();
            foreach (Guid companionId in Companions)
            {
                lCompanions.Add((Hero)CoopObjectManager.GetObject(companionId));
            }
            foreach (Guid supporterId in Supporters)
            {
                lSupporters.Add((Hero)CoopObjectManager.GetObject(supporterId));
            }
            foreach (Guid commanderheroesId in CommanderHeroes)
            {
                lCommanderHeroes.Add((Hero)CoopObjectManager.GetObject(commanderheroesId));
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(newClan, CoopObjectManager.GetObject(id));
            }
        }
    }
}
