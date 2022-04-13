using System.Collections.Generic;
using JetBrains.Annotations;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RemoteAction;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Base class for <see cref="RailEvent" /> that represent a remote action such as a
    ///     method call or a field change.
    /// </summary>
    public abstract class EventActionBase : RailEvent, ISynchronizedAction
    {
        /// <summary>
        ///     Returns the arguments that are passed to the remove action execution.
        ///     For method calls:   The arguments to the method call
        ///     For field changes:  The new value
        /// </summary>
        public abstract IEnumerable<Argument> Arguments { get; }

        /// <summary>
        ///     Returns whether action is valid or not.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsValid();

        /// <summary>
        ///     If set, the events scope will be defined by the given entities. For
        ///     more details <see cref="CoopRailScopeEvaluator"/>.
        /// </summary>
        [CanBeNull] public abstract IEnumerable<EntityId> AffectedEntities {  get; }
    }
}