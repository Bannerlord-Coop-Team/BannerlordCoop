using Common;
using System;

namespace GameInterface.Services.GameDebug.Metrics;

public interface IPartySyncPerformanceLogger : IGameAbstraction
{
    bool IsEnabled { get; }
    string Enable(TimeSpan interval, string fileName);
    string Disable();
    string Status();
    void HandleSnapshot(NetworkPartySyncPerformanceSnapshot snapshot);
}
