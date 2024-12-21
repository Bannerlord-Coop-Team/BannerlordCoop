using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.WeaponDesigns.Messages.Lifetime
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateWeaponDesign : ICommand
    {
        [ProtoMember(1)]
        public string WeaponDesignId { get; set; }

        public NetworkCreateWeaponDesign(string weaponDesignId)
        {
            WeaponDesignId = weaponDesignId;
        }
    }
}
