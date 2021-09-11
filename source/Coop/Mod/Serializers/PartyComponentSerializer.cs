using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        private Hero hero;

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
                case LordPartyComponent lordPartyComponent:
                    partyComponentSerializer = new LordPartyComponentSerializer(lordPartyComponent);
                    break;
                case BanditPartyComponent banditPartyComponent:
                    partyComponentSerializer = new BanditPartyComponentSerializer(banditPartyComponent);
                    break;
                case CustomPartyComponent customPartyComponent:
                    partyComponentSerializer = new CustomPartyComponentSerializer(customPartyComponent);
                    break;
                default:
                    throw new ArgumentException($"{partyComponent.GetType()} is not serializable by {nameof(PartyComponentSerializer)}.");
            }
        }

        public object Deserialize()
        {
            if (partyComponentSerializer is LordPartyComponentSerializer lordPartyComponentSerializer)
            {
                lordPartyComponentSerializer.SetHeroReference(hero);
            }

            PartyComponent partyComponent = (PartyComponent)partyComponentSerializer.Deserialize();

            if(party != null)
            {
                typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty)).SetValue(partyComponent, party.Deserialize());
            }

            return partyComponent;
        }

        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }
    }

    [Serializable]
    class BanditPartyComponentSerializer : ICustomSerializer
    {
        MBObjectSerializer hideout;
        bool isBossParty;

        public BanditPartyComponentSerializer(BanditPartyComponent component)
        {
            hideout = new MBObjectSerializer(component.Hideout);
            isBossParty = component.IsBossParty;
        }

        public object Deserialize()
        {
            Hideout hideout = (Hideout)this.hideout.Deserialize();

            return Activator.CreateInstance(
                typeof(BanditPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { hideout, isBossParty },
                null);
        }
    }

    [Serializable]
    class LordPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        private Hero hero;

        MBObjectSerializer owner;
        

        public LordPartyComponentSerializer(LordPartyComponent component)
        {
            owner = new MBObjectSerializer(component.Owner);
        }

        public object Deserialize()
        {
            Hero owner = (Hero)this.owner.Deserialize();

            if(owner == null)
            {
                owner = hero;
            }

            return Activator.CreateInstance(
                typeof(LordPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { owner },
                null);
        }

        public void SetHeroReference(Hero hero)
        {
            this.hero = hero; 
        }
    }

    [Serializable]
    class CustomPartyComponentSerializer : ICustomSerializer
    {
        private MBObjectSerializer owner;
        private string name;
        private MBObjectSerializer settlement;
        public CustomPartyComponentSerializer(CustomPartyComponent customPartyComponent)
        {
            owner = new MBObjectSerializer(customPartyComponent.PartyOwner);
            settlement = new MBObjectSerializer(customPartyComponent.HomeSettlement);
            name = customPartyComponent.Name.ToString();
        }

        public object Deserialize()
        {
            CustomPartyComponent component = (CustomPartyComponent)
                Activator.CreateInstance(
                typeof(CustomPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[0],
                null);

            typeof(CustomPartyComponent).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(component, owner.Deserialize());
            typeof(CustomPartyComponent).GetField("_homeSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(component, settlement.Deserialize());

            TextObject newName = new TextObject(name);

            typeof(CustomPartyComponent).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(component, newName);

            return component;
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
}