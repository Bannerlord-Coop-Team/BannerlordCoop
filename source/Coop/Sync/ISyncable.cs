using JetBrains.Annotations;

namespace Coop.Sync
{
    public interface ISyncable
    {
        void Apply([CanBeNull] object state, [CanBeNull] object value);
    }
}
