using System.Collections.Generic;

namespace RemoteAction
{
    public interface ISynchronizedAction
    {
        IEnumerable<Argument> Arguments { get; }

        bool IsValid();
    }
}