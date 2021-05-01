using JetBrains.Annotations;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Describes a behaviour that only applies when a condition is met.
    /// </summary>
    public abstract class ConditionalBehaviour
    {
        public readonly Condition Condition;

        /// <summary>
        ///     Constructs a new conditional behaviour.
        /// </summary>
        /// <param name="condition"></param>
        public ConditionalBehaviour([CanBeNull] Condition condition)
        {
            Condition = condition;
        }

        /// <summary>
        ///     Evaluates the condition.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public bool AppliesTo(EOriginator origin, object instance)
        {
            return Condition == null || Condition.Evaluate(origin, instance);
        }
    }
}