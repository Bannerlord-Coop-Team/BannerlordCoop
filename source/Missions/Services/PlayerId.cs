using System;

namespace Missions.Services
{
    public struct PlayerId
    {
        public Guid Id;
        public PlayerId(Guid id)
        {
            Id = id;
        }
    }
}
