﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sync.Value;

namespace Sync.Behaviour
{
    /// <summary>
    ///     Builder class to define the behaviour of a managed field.
    /// </summary>
    public class FieldBehaviourBuilder : ConditionalBehaviour
    {
        #region Private

        private readonly IEnumerable<FieldId> m_FieldIds;

        #endregion

        public FieldBehaviourBuilder(IEnumerable<FieldId> fieldIds, Condition condition) : base(condition)
        {
            m_FieldIds = fieldIds;
        }

        /// <summary>
        ///     The action to be taken after the field was changed through a known accessor.
        /// </summary>
        public EFieldChangeAction Action { get; private set; } = EFieldChangeAction.Keep;

        /// <summary>
        ///     Whether or not the change shall be broadcast sent to the server in order to broadcast it.
        /// </summary>
        public bool DoBroadcast { get; private set; }

        public Func<ISynchronization> SynchronizationFactory { get; private set; }

        /// <summary>
        ///     The changed field value will be broadcast to all clients as an authoritative change. The change
        ///     will be applied to the field directly, i.e. not trough any method or property. All clients will
        ///     receive the changed value on the same campaign tick. The originator of the call will receive the
        ///     authoritative change as well.
        /// </summary>
        public FieldBehaviourBuilder Broadcast([NotNull] Func<ISynchronization> syncFactory,
            [NotNull] IActionValidator validator = null)
        {
            SynchronizationFactory = syncFactory;
            DoBroadcast = true;
            if (validator != null)
                foreach (var id in m_FieldIds)
                    ActionValidatorRegistry.Register(id, validator);
            return this;
        }

        /// <summary>
        ///     The change to the field is kept.
        /// </summary>
        public void Keep()
        {
            Action = EFieldChangeAction.Keep;
        }

        /// <summary>
        ///     The change to the field is reverted.
        /// </summary>
        public void Revert()
        {
            Action = EFieldChangeAction.Revert;
        }
    }
}