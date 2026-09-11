#if DEBUG
using Autofac;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Missions.Battles;

public interface INavalMissionAdapterLoader : IDisposable
{
    INavalMissionAdapter Load();
}

public sealed class NavalMissionAdapterLoader : INavalMissionAdapterLoader
{
    private readonly ILifetimeScope parent;
    private ILifetimeScope scope;
    public NavalMissionAdapterLoader(ILifetimeScope parent) => this.parent = parent;
    public INavalMissionAdapter Load()
    {
        if (scope != null) throw new InvalidOperationException("The naval adapter is already loaded.");
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "NavalDLC"))
            throw new InvalidOperationException("Enable the installed NavalDLC module before starting this DEBUG lab.");
        var path = Path.Combine(Path.GetDirectoryName(typeof(MissionModule).Assembly.Location), "Missions.Naval.dll");
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetType("Missions.Naval.NavalMissionAdapter", throwOnError: true);
        scope = parent.BeginLifetimeScope(builder => builder.RegisterType(type)
            .As<INavalMissionAdapter>().InstancePerDependency());
        return scope.Resolve<INavalMissionAdapter>();
    }
    public void Dispose()
    {
        scope?.Dispose();
        scope = null;
    }
}
#endif
