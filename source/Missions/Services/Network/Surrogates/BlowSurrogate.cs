using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class BlowSurrogate
    {
        [ProtoMember(1)]
        public byte[] data { get; }

        public BlowSurrogate(Blow obj)
        {
            if (obj.Equals(default(Blow))) return;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return;

            IBinaryPackage package = packageFactory.GetBinaryPackage(obj);

            data = BinaryFormatterSerializer.Serialize(package);
        }

        private Blow Deserialize()
        {
            if (data == null) return default;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return default;

            var package = BinaryFormatterSerializer.Deserialize<BlowBinaryPackage>(data);

            return package.Unpack<Blow>(packageFactory);
        }

        public static implicit operator BlowSurrogate(Blow obj)
        {
            return new BlowSurrogate(obj);
        }

        public static implicit operator Blow(BlowSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
