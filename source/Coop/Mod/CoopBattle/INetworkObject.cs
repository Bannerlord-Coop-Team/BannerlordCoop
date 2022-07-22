using System;

namespace Coop.Mod.CoopBattle
{
    public interface INetworkObject
    {

        Guid NetworkId { get; set; }

        void Destroy(Type type);
    }
}