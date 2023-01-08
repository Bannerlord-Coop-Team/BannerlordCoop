using System;

namespace Missions
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
