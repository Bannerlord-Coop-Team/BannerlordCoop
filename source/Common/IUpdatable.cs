using System;

namespace Common
{
    public interface IUpdateable
    {
        void Update(TimeSpan frameTime);
    }
}
