using JetBrains.Annotations;
using Sync.Call;
using Sync.Value;
using System;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Interface to synchronize method calls and field changes to other clients.
    /// </summary>
    public interface ISynchronization
    {
        /// <summary>
        ///     Broadcast a method call to all clients.
        /// </summary>
        /// <param name="id">Id of the method to call.</param>
        /// <param name="instance">Instance to call the method on. null for a static call.</param>
        /// <param name="args">Method call arguments.</param>
        void Broadcast(InvokableId id, object instance, object[] args);

        /// <summary>
        ///     Broadcast a method call that is associated with one or more entities. The server will send
        ///     the method call only to those clients, that have at least one of the affected entities in
        ///     scope.
        ///     For more details on scoping, <see cref="CoopRailScopeEvaluator"/>.
        /// </summary>
        /// <param name="affectedEntities">The entitites affected by this call or null.</param>
        /// <param name="id">Id of the method to call.</param>
        /// <param name="instance">Instance to call the method on. null for a static call.</param>
        /// <param name="args">Method call arguments.</param>
        void Broadcast(Guid[] affectedEntities, InvokableId id, object instance, object[] args);

        /// <summary>
        ///     Broadcast a field change buffer to all clients.
        /// </summary>
        /// <param name="buffer">The buffer whose content should be broadcast.</param>
        void Broadcast(FieldChangeBuffer buffer);
    }
}