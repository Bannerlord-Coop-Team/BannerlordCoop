using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Registry.Auto;
public class AutoRegistryCallbacks<T> where T : class
{
    public Action<T, string> ServerCreatedCallback { get; }
    public Action<T, string> ServerDestroyedCallback { get; }
    public Action<T, string> ClientCreatedCallback { get; }
    public Action<T, string> ClientDestroyedCallback { get; }

    public AutoRegistryCallbacks(IAutoRegistry<T> autoRegistry)
    {
        ServerCreatedCallback = autoRegistry.OnServerCreated;
        ServerDestroyedCallback = autoRegistry.OnServerDestroyed;
        ClientCreatedCallback = autoRegistry.OnClientCreated;
        ClientDestroyedCallback = autoRegistry.OnClientDestroyed;
    }
}
