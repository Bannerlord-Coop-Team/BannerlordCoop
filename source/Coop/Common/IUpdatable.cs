using System;

namespace Coop.Common
{
    public interface IUpdateable
    {
        void Update(TimeSpan frameTime);
    }
}
