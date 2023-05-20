using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Messaging
{
    /// <summary>
    /// Response to a <see cref="ICommand"/>
    /// </summary>
    public interface IResponse : IMessage
    {
    }
}
