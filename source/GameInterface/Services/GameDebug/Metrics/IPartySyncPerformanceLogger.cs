using Common;
using System;

namespace GameInterface.Services.GameDebug.Metrics;

public interface IPartySyncPerformanceLogger : IGameAbstraction
{
    string Enable(TimeSpan interval, string fileName);
    string Disable();
    string Status();
    void HandleSnapshot(NetworkPartySyncPerformanceSnapshot snapshot);
}
