using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace Missions.Services.Network.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterObjectSurrogate
    {
        [ProtoMember(1)]
        public byte[] data { get; }

        public CharacterObjectSurrogate(CharacterObject obj)
        {
            if (obj == null) return;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return;

            IBinaryPackage package = packageFactory.GetBinaryPackage(obj);

            data = BinaryFormatterSerializer.Serialize(package);
        }

        private CharacterObject Deserialize()
        {
            if (data == null) return default;

            if (ContainerProvider.TryResolve(out IBinaryPackageFactory packageFactory) == false) return default;

            var package = BinaryFormatterSerializer.Deserialize<CharacterObjectBinaryPackage>(data);

            return package.Unpack<CharacterObject>(packageFactory);
        }

        public static implicit operator CharacterObjectSurrogate(CharacterObject obj)
        {
            return new CharacterObjectSurrogate(obj);
        }

        public static implicit operator CharacterObject(CharacterObjectSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
