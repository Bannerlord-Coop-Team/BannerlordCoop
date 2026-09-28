using System;

namespace Common.Audit;
public interface IAuditor : IDisposable
{
    string Audit();
}
