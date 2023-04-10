using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization.Native;
using ProtoBuf;
using Serilog.Core;
using System;
using System.Diagnostics;
using TaleWorlds.Core;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkWeaponPickedup : INetworkEvent
    {   
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public ItemObject ItemObject
        {
            get { return UnpackItemObject(); }
            set { _packedItemObject = PackItemObject(value); }
        }
        [ProtoMember(3)]
        private byte[] _packedItemObject;
        private ItemObject _itemObject;
        private readonly IBinaryPackageFactory packageFactory;

        public ItemModifier ItemModifier
        {
            get { return UnpackItemModifier(); }
            set { _packedItemModifier = PackItemModifier(value); }
        }
        [ProtoMember(4)]
        private byte[] _packedItemModifier;
        private ItemModifier _itemModifier;
        [ProtoMember(5)]
        private bool isItemModifierNull = false;

        public Banner Banner
        {
            get { return UnpackBanner(); }
            set { _packedBanner = PackBanner(value); }    
        }
        [ProtoMember(6)]
        private byte[] _packedBanner;
        private Banner _banner;
        [ProtoMember(7)]
        private bool isBannerNull = false;

        public NetworkWeaponPickedup(IBinaryPackageFactory packageFactory, Guid agentId, EquipmentIndex equipmentIndex, ItemObject weaponObject, ItemModifier itemModifier, Banner banner)
        {
            this.packageFactory = packageFactory;
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            ItemObject = weaponObject;
            ItemModifier = itemModifier;
            Banner = banner;
        }

        public NetworkWeaponPickedup() { }

        private byte[] PackItemObject(ItemObject itemObject)
        {
            var itemObjectPackage = new ItemObjectBinaryPackage(itemObject, packageFactory);
            itemObjectPackage.Pack();
            return BinaryFormatterSerializer.Serialize(itemObjectPackage);
        }

        private ItemObject UnpackItemObject()
        {
            if (_itemObject != null) return _itemObject;

            var itemObject = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(_packedItemObject);

            _itemObject = itemObject.Unpack<ItemObject>(packageFactory);
            return _itemObject;
        }

        private byte[] PackItemModifier(ItemModifier itemModifier)
        {
            if (itemModifier == null)
            {
                isItemModifierNull = true;
                return Array.Empty<byte>();
            }

            var itemModifierPackage = new ItemModifierBinaryPackage(itemModifier, packageFactory); 

            itemModifierPackage.Pack();

            return BinaryFormatterSerializer.Serialize(itemModifierPackage);
        }

        private ItemModifier UnpackItemModifier()
        {
            if (_itemModifier != null) return _itemModifier;

            if (isItemModifierNull) return null;

            var itemModifier = BinaryFormatterSerializer.Deserialize<ItemModifierBinaryPackage>(_packedItemModifier);

            _itemModifier = itemModifier.Unpack<ItemModifier>(packageFactory);
            return _itemModifier;
        }

        private byte[] PackBanner(Banner banner)
        {
            if (banner == null)
            {
                isBannerNull = true;
                return Array.Empty<byte>();
            }
            var bannerPackage = new BannerBinaryPackage(banner, packageFactory);
            bannerPackage.Pack();

            return BinaryFormatterSerializer.Serialize(bannerPackage);
        }

        private Banner UnpackBanner()
        {
            if(_banner != null) return _banner;
            if (isBannerNull) return null;

            var banner = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(_packedBanner);

            _banner = banner.Unpack<Banner>(packageFactory);

            return _banner;
        }
    }
}