namespace Sync.Behaviour
{
    public interface IPendingAction
    {
        void Execute();
        void Broadcast();
    }
}