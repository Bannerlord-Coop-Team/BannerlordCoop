using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        private Hero hero;

        private ICustomSerializer partyComponentSerializer;
        private Guid hero;
        private Guid party;
        

        public PartyComponentSerializer(PartyComponent partyComponent)
        {
            if(partyComponent.MobileParty != null)
            {
                party = CoopObjectManager.GetGuid(partyComponent.MobileParty);
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
            component = (PartyComponent)partyComponentSerializer.Deserialize();

            return component;
        }

        public void ResolveReferenceGuids()
        {
            typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty)).SetValue(component, CoopObjectManager.GetObject(party));
            partyComponentSerializer.ResolveReferenceGuids();
        }
    }

    [Serializable]
    class BanditPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public BanditPartyComponent BanditPartyComponent;

        Guid hideout;
        bool isBossParty;

        public BanditPartyComponentSerializer(BanditPartyComponent component)
        {
            hideout = CoopObjectManager.GetGuid(component.Hideout);
            isBossParty = component.IsBossParty;
        }

        public object Deserialize()
        {
            BanditPartyComponent = (BanditPartyComponent)Activator.CreateInstance(
                typeof(BanditPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null, isBossParty },
                null);

            return BanditPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Hideout").GetSetMethod(true).Invoke(BanditPartyComponent, new object[] { CoopObjectManager.GetObject(hideout) });
        }
    }

    [Serializable]
    class LordPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public LordPartyComponent LordPartyComponent;

        Guid owner;
        

        public LordPartyComponentSerializer(LordPartyComponent component)
        {
            owner = CoopObjectManager.GetGuid(component.Owner);
        }

        public object Deserialize()
        {
            LordPartyComponent = (LordPartyComponent)Activator.CreateInstance(
                typeof(LordPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null },
                null);
            return LordPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Owner").GetSetMethod(true).Invoke(LordPartyComponent, new object[] { CoopObjectManager.GetObject(owner) });
        }
    }

    [Serializable]
    class CustomPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public CustomPartyComponent CustomPartyComponent;

        private string name;
        private Guid owner;
        private Guid settlement;
        public CustomPartyComponentSerializer(CustomPartyComponent customPartyComponent)
        {
            owner = CoopObjectManager.GetGuid(customPartyComponent.PartyOwner);
            settlement = CoopObjectManager.GetGuid(customPartyComponent.HomeSettlement);
            name = customPartyComponent.Name.ToString();
        }

        public object Deserialize()
        {
            CustomPartyComponent = (CustomPartyComponent)
                Activator.CreateInstance(
                typeof(CustomPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[0],
                null);

            TextObject newName = new TextObject(name);

            typeof(CustomPartyComponent).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, newName);

            return CustomPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CustomPartyComponent).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, CoopObjectManager.GetObject(owner));
            typeof(CustomPartyComponent).GetField("_homeSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, CoopObjectManager.GetObject(settlement));
        }
    }

    [Serializable]
    class CaravanPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public CaravanPartyComponent CaravanPartyComponent;

        private Guid owner;
        private Guid settlement;
        public CaravanPartyComponentSerializer(CaravanPartyComponent caravanPartyComponent)
        {
            owner = CoopObjectManager.GetGuid(caravanPartyComponent.Owner);
            settlement = CoopObjectManager.GetGuid(caravanPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            CaravanPartyComponent = (CaravanPartyComponent)Activator.CreateInstance(
                typeof(CaravanPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic, 
                null, 
                new object[] { null, null },
                null);
            return CaravanPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Settlement").GetSetMethod(true).Invoke(CaravanPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
            typeof(CaravanPartyComponent).GetProperty("Owner").GetSetMethod(true).Invoke(CaravanPartyComponent, new object[] { CoopObjectManager.GetObject(owner) });
        }
    }

    [Serializable]
    class VillagerPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public VillagerPartyComponent VillagerPartyComponent;
        private Guid village;
        public VillagerPartyComponentSerializer(VillagerPartyComponent villagerPartyComponent)
        {
            village = CoopObjectManager.GetGuid(villagerPartyComponent.Village);
        }

        public object Deserialize()
        {
            VillagerPartyComponent = (VillagerPartyComponent)Activator.CreateInstance(
                typeof(VillagerPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null },
                null);
            return VillagerPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Village").GetSetMethod(true).Invoke(VillagerPartyComponent, new object[] { CoopObjectManager.GetObject(village) });
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

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    class GarrisonPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public GarrisonPartyComponent GarrisonPartyComponent;
        private Guid settlement;
        public GarrisonPartyComponentSerializer(GarrisonPartyComponent garrisonPartyComponent)
        {
            settlement = CoopObjectManager.GetGuid(garrisonPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            GarrisonPartyComponent = (GarrisonPartyComponent)Activator.CreateInstance(
                typeof(GarrisonPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null },
                null);
            return GarrisonPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Settlement").GetSetMethod(true).Invoke(GarrisonPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
        }
    }

    [Serializable]
    class MilitiaPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public MilitiaPartyComponent MilitiaPartyComponent;
        private Guid settlement;
        public MilitiaPartyComponentSerializer(MilitiaPartyComponent militiaPartyComponent)
        {
            settlement = CoopObjectManager.GetGuid(militiaPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            MilitiaPartyComponent = (MilitiaPartyComponent)Activator.CreateInstance(
                typeof(MilitiaPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null },
                null);
            return MilitiaPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            typeof(CaravanPartyComponent).GetProperty("Settlement").GetSetMethod(true).Invoke(MilitiaPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
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