using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.WeaponDesigns.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateWeaponDesign : ICommand
    {
        [ProtoMember(1)]
        public string WeaponDesignId { get; set; }
        [ProtoMember(2)]
        public uint Handle { get; set; }

        public NetworkCreateWeaponDesign(string weaponDesignId, uint handle)
        {
            WeaponDesignId = weaponDesignId;
            Handle = handle;
        }
    }
}
