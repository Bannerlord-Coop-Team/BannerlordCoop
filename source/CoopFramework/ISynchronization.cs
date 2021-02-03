using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RemoteAction;
using Sync;

namespace CoopFramework
{
    public interface ISynchronization
    {
        #region Debugging

        /// <summary>
        ///     Returns the call history of <see cref="Broadcast" />.
        /// </summary>
        [NotNull]
        CallStatistics BroadcastHistory { get; }

        #endregion


        void Broadcast(MethodId id, object instance, object[] args);
        void Broadcast(FieldChangeBuffer buffer);
    }
}