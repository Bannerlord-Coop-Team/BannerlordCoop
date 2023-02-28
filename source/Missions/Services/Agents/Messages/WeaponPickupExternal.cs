using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using System.Diagnostics;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract]
    public class WeaponPickupExternal : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        [ProtoMember(3)]
        private byte[] _packedItemObject;

        [ProtoMember(4)]
        private byte[] _packedItemModifier;

        [ProtoMember(5)]
        private byte[] _packedBanner;

        private ItemObject _itemObject; 
        private ItemModifier _itemModifier;
        private Banner _banner;

        public ItemObject ItemObject
        {
            get { return UnpackItemObject(); }
            set { _packedItemObject = PackItemObject(value); }
        }

        public ItemModifier ItemModifier
        {
            get { return UnpackItemModifier(); }
            set { _packedItemModifier = PackItemModifier(value); }
        }

        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }    
        }

        public WeaponPickupExternal(Guid agentId, EquipmentIndex equipmentIndex, ItemObject weaponObject, ItemModifier itemModifier, Banner banner)
        {
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }

        public WeaponPickupExternal() { }

        private byte[] PackItemObject(ItemObject itemObject)
        {
            var factory = new BinaryPackageFactory();
            var itemObjectPackage = new ItemObjectBinaryPackage(itemObject, factory);
            itemObjectPackage.Pack();
            System.Diagnostics.Debug.WriteLine("packedItemObject");
            return BinaryFormatterSerializer.Serialize(itemObjectPackage);
        }

        private ItemObject UnpackItemObject()
        {
            var factory = new BinaryPackageFactory();
            var itemObject = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(_packedItemObject);
            itemObject.BinaryPackageFactory = factory;

            _itemObject = itemObject.Unpack<ItemObject>();

            System.Diagnostics.Debug.WriteLine("UnpackedItemObject");
            return _itemObject;
        }

        private byte[] PackItemModifier(ItemModifier itemModifier)
        {
            var factory = new BinaryPackageFactory();
            var itemModifierPackage = new ItemModifierBinaryPackage(itemModifier, factory);
            itemModifierPackage.Pack();

            System.Diagnostics.Debug.WriteLine("packedItemMod");
            return BinaryFormatterSerializer.Serialize(itemModifierPackage);
        }

        private ItemModifier UnpackItemModifier()
        {
            var factory = new BinaryPackageFactory();
            var itemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);
            itemModifier.BinaryPackageFactory = factory;

            _itemModifier = itemModifier.Unpack<ItemModifier>();
            System.Diagnostics.Debug.WriteLine("unpackedItemMod");
            return _itemModifier;
        }


        private byte[] PackBanner(Banner banner)
        {
            var factory = new BinaryPackageFactory();
            var bannerPackage = new BannerBinaryPackage(banner, factory);
            bannerPackage.Pack();

            System.Diagnostics.Debug.WriteLine("packedBanner");
            return BinaryFormatterSerializer.Serialize(bannerPackage);
        }

        private Banner UnpackBanner()
        {
            var factory = new BinaryPackageFactory();
            var banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);
            banner.BinaryPackageFactory = factory;

            _banner = banner.Unpack<Banner>();

            System.Diagnostics.Debug.WriteLine("unpackedBanner");
            return _banner;
        }
    }
}