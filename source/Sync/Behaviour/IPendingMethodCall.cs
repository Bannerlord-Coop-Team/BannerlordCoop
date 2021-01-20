using JetBrains.Annotations;

namespace Sync.Behaviour
{
    public interface IPendingMethodCall
    {
        void Broadcast();
        /// <summary>
        /// The instance the method is called on. null for static method calls.
        /// </summary>
        [CanBeNull] object Instance { get; }
        [NotNull] object[] Parameters { get; }
    }
}