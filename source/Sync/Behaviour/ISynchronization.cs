using Sync;

namespace CoopFramework
{
    public interface ISynchronization
    {
        void Broadcast(MethodId id, object instance, object[] args);
        void Broadcast(FieldChangeBuffer buffer);
    }
}