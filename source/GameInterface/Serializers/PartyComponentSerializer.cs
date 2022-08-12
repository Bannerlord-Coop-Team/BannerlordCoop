using Common;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    internal class PartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        private PartyComponent component;

        private ICustomSerializer partyComponentSerializer;
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
            component = (PartyComponent)partyComponentSerializer.Deserialize();

            return component;
        }

        public void ResolveReferenceGuids()
        {
            if (component == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            // TODO remove expanded code

            typeof(PartyComponent)
                .GetProperty("MobileParty")
                .GetSetMethod(true)
                .Invoke(component, new object[] { CoopObjectManager.GetObject(party) });
            partyComponentSerializer.ResolveReferenceGuids();
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
            if (BanditPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            BanditPartyComponent.GetType()
                .GetProperty(nameof(BanditPartyComponent.Hideout))
                .GetSetMethod(true)
                .Invoke(BanditPartyComponent, new object[] { CoopObjectManager.GetObject(hideout) });
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
                new object[] { null, null },
                null);
            return LordPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            if (LordPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            LordPartyComponent.GetType()
                .GetProperty(nameof(LordPartyComponent.Owner))
                .GetSetMethod(true)
                .Invoke(LordPartyComponent, new object[] { CoopObjectManager.GetObject(owner) });

            // TODO: Is it correct to set the owner as leader?
            LordPartyComponent.GetType()
                .GetField("_leader", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(LordPartyComponent, CoopObjectManager.GetObject(owner));
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
            CustomPartyComponent = (CustomPartyComponent)Activator.CreateInstance(
                typeof(CustomPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[0],
                null);

            TextObject newName = new TextObject(name);

            CustomPartyComponent.GetType()
                .GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, newName);

            return CustomPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            if (CustomPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            CustomPartyComponent.GetType()
                .GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, CoopObjectManager.GetObject(owner));
            CustomPartyComponent.GetType()
                .GetField("_homeSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CustomPartyComponent, CoopObjectManager.GetObject(settlement));
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
                new object[] { null, null, null },
                null);
            return CaravanPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            if (CaravanPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            CaravanPartyComponent.GetType()
                .GetProperty(nameof(CaravanPartyComponent.Settlement))
                .GetSetMethod(true)
                .Invoke(CaravanPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
            typeof(CaravanPartyComponent).GetProperty("Owner").GetSetMethod(true).Invoke(CaravanPartyComponent, new object[] { CoopObjectManager.GetObject(owner) });
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
            if (VillagerPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            VillagerPartyComponent.GetType()
                .GetProperty(nameof(VillagerPartyComponent.Village))
                .GetSetMethod(true)
                .Invoke(VillagerPartyComponent, new object[] { CoopObjectManager.GetObject(village) });
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    class CommonAreaPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        CommonAreaPartyComponent newCommonAreaPartyComponent;

        CommonAreaSerializer commonArea;
        Guid owner;
        Guid settlement;
        public CommonAreaPartyComponentSerializer(CommonAreaPartyComponent commonAreaPartyComponent)
        {
            commonArea = new CommonAreaSerializer(commonAreaPartyComponent.CommonArea);
            owner = CoopObjectManager.GetGuid(commonAreaPartyComponent.Owner);
            settlement = CoopObjectManager.GetGuid(commonAreaPartyComponent.Settlement);
        }

        public object Deserialize()
        {
            newCommonAreaPartyComponent = (CommonAreaPartyComponent)Activator.CreateInstance(
                typeof(CommonAreaPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { null, null, commonArea.Deserialize() },
                null);
            return newCommonAreaPartyComponent;
        }

        public void ResolveReferenceGuids()
        {
            if (newCommonAreaPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            newCommonAreaPartyComponent.GetType()
                .GetProperty(nameof(newCommonAreaPartyComponent.Owner))
                .GetSetMethod(true)
                .Invoke(newCommonAreaPartyComponent, new object[]
                {
                    CoopObjectManager.GetObject<Hero>(owner)
                });

            newCommonAreaPartyComponent.GetType()
                .GetProperty(nameof(newCommonAreaPartyComponent.Settlement))
                .GetSetMethod(true)
                .Invoke(newCommonAreaPartyComponent, new object[]
                {
                    CoopObjectManager.GetObject<Settlement>(settlement)
                });
        }

        public byte[] Serialize()
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
            if (GarrisonPartyComponent == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            GarrisonPartyComponent.GetType()
                .GetProperty(nameof(GarrisonPartyComponent.Settlement))
                .GetSetMethod(true)
                .Invoke(GarrisonPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
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
            MilitiaPartyComponent.GetType()
                .GetProperty(nameof(MilitiaPartyComponent.Settlement))
                .GetSetMethod(true)
                .Invoke(MilitiaPartyComponent, new object[] { CoopObjectManager.GetObject(settlement) });
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}