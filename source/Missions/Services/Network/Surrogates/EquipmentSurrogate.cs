using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Services.Network.Surrogates
{
    /// <summary>
    /// Surrogate for the Equipment Class
    /// </summary>

    [ProtoContract(SkipConstructor = true)]
    public class EquipmentSurrogate
    {
        [ProtoMember(1)]
        public byte[] data { get; }

        public EquipmentSurrogate(Equipment obj)
        {
            if (obj == null) return;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return;

            IBinaryPackage package = packageFactory.GetBinaryPackage(obj);

            data = BinaryFormatterSerializer.Serialize(package);
        }

        private Equipment Deserialize()
        {
            if (data == null) return default;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return default;

            var package = BinaryFormatterSerializer.Deserialize<EquipmentBinaryPackage>(data);

            return package.Unpack<Equipment>(packageFactory);
        }

        public static implicit operator EquipmentSurrogate(Equipment obj)
        {
            return new EquipmentSurrogate(obj);
        }

        public static implicit operator Equipment(EquipmentSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
