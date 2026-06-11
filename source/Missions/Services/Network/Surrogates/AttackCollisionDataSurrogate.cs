using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class AttackCollisionDataSurrogate
    {
        [ProtoMember(1)]
        public byte[] data { get; }

        public AttackCollisionDataSurrogate(AttackCollisionData obj)
        {
            if (obj.Equals(default(AttackCollisionData))) return;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return;

            IBinaryPackage package = packageFactory.GetBinaryPackage(obj);

            data = BinaryFormatterSerializer.Serialize(package);
        }

        private AttackCollisionData Deserialize()
        {
            if (data == null) return default;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return default;

            var package = BinaryFormatterSerializer.Deserialize<AttackCollisionDataBinaryPackage>(data);

            return package.Unpack<AttackCollisionData>(packageFactory);
        }

        public static implicit operator AttackCollisionDataSurrogate(AttackCollisionData obj)
        {
            return new AttackCollisionDataSurrogate(obj);
        }

        public static implicit operator AttackCollisionData(AttackCollisionDataSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
