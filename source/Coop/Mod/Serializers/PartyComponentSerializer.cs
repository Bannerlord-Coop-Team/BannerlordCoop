using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PartyComponentSerializer : ICustomSerializer
    {
        private ICustomSerializer partyComponentSerializer;
        private MBObjectSerializer party; 

        public PartyComponentSerializer(PartyComponent partyComponent)
        {
            if(partyComponent.MobileParty != null)
            {
                party = new MBObjectSerializer(partyComponent.MobileParty);
            }

            switch (partyComponent)
            {
                case CaravanPartyComponent caravanPartyComponent:
                    partyComponentSerializer = new CaravanPartyComponentSerializer(caravanPartyComponent);
                    break;
                case VillagerPartyComponent villagerPartyComponent:
                    partyComponentSerializer = new VillagerPartyComponentSerializer(villagerPartyComponent);
                    break;
                case CommonAreaPartyComponent commonAreaPartyComponent:
                    partyComponentSerializer = new CommonAreaPartyComponentSerializer(commonAreaPartyComponent);
                    break;
                case GarrisonPartyComponent garrisonPartyComponent:
                    partyComponentSerializer = new GarrisonPartyComponentSerializer(garrisonPartyComponent);
                    break;
                case MilitiaPartyComponent militiaPartyComponent:
                    partyComponentSerializer = new MilitiaPartyComponentSerializer(militiaPartyComponent);
                    break;
                case WarPartyComponent warPartyComponent:
                    partyComponentSerializer = new WarPartyComponentSerializer(warPartyComponent);
                    break;
                default:
                    throw new ArgumentException($"{partyComponent.GetType()} is not serializable by {nameof(PartyComponentSerializer)}.");
            }
        }

        public object Deserialize()
        {
            PartyComponent partyComponent = (PartyComponent)partyComponentSerializer.Deserialize();
            if(party != null)
            {
                typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty)).SetValue(partyComponent, party.Deserialize());
            }

            return partyComponent;
        }
    }

    [Serializable]
    class CaravanPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer owner;
        private MBObjectSerializer settlement;
        public CaravanPartyComponentSerializer(CaravanPartyComponent caravanPartyComponent)
        {
            owner = new MBObjectSerializer(caravanPartyComponent.Owner);
            settlement = new MBObjectSerializer(caravanPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            return Activator.CreateInstance(
                typeof(CaravanPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic, 
                null, 
                new object[] { settlement.Deserialize(), owner.Deserialize() },
                null);
        }
    }

    [Serializable]
    class VillagerPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer village;
        public VillagerPartyComponentSerializer(VillagerPartyComponent villagerPartyComponent)
        {
            village = new MBObjectSerializer(villagerPartyComponent.Village);
        }

        public object Deserialize()
        {
            return Activator.CreateInstance(
                typeof(VillagerPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { village.Deserialize() },
                null);
        }
    }

    [Serializable]
    class CommonAreaPartyComponentSerializer : ICustomSerializer
    {
        public CommonAreaPartyComponentSerializer(CommonAreaPartyComponent commonAreaPartyComponent)
        {
        }

        public object Deserialize()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    class GarrisonPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer settlement;
        public GarrisonPartyComponentSerializer(GarrisonPartyComponent garrisonPartyComponent)
        {
            settlement = new MBObjectSerializer(garrisonPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            return Activator.CreateInstance(
                typeof(GarrisonPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { settlement.Deserialize() },
                null);
        }
    }

    [Serializable]
    class MilitiaPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer settlement;
        public MilitiaPartyComponentSerializer(MilitiaPartyComponent militiaPartyComponent)
        {
            settlement = new MBObjectSerializer(militiaPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            return Activator.CreateInstance(
                typeof(MilitiaPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { settlement.Deserialize() },
                null);
        }
    }

    [Serializable]
    class WarPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer clan;
        public WarPartyComponentSerializer(WarPartyComponent warPartyComponent)
        {
            clan = new MBObjectSerializer(warPartyComponent.Clan);
        }

        public object Deserialize()
        {
            return Activator.CreateInstance(
                typeof(WarPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { clan.Deserialize() },
                null);
        }
    }
}