using System.Collections.Generic;

namespace RemoteAction
{
    public interface IAction
    {
        IEnumerable<Argument> Arguments { get; }
    }
}