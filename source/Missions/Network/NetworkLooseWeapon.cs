using System;

namespace Missions.Network
{
    public class NetworkLooseWeapon : INetworkObject
    {
        public Guid networkId;

        public Guid NetworkId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Destroy(Type type)
        {
            throw new NotImplementedException();
        }

        public void Move()
        {
            throw new NotImplementedException();
        }

        public void OnDeath()
        {
            throw new NotImplementedException();
        }
    }
}
