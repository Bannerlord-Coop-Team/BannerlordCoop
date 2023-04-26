using System;

namespace Common.Messaging
{
    /// <summary>
    /// A command drives functionality rather than reacting to it like
    /// <see cref="IEvent"/>
    /// </summary>
    /// <inheritdoc/>
    public interface ICommand : IInternalMessage
    {
        Guid TransactionID { get; }
    }
}
