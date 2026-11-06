using Autofac;
using Common;
using GameInterface;
using GameInterface.Services.Modules;
using System.IO;

namespace Coop.Core;

/// <summary>
/// Registers the modules of the optional naval assemblies while War Sails co-op runs, because they bind to NavalDLC types.
/// </summary>
public class NavalAssemblyModule : Module
{
    private readonly string[] assemblyFiles;

    // Takes the naval assemblies this role's container needs.
    public NavalAssemblyModule(params string[] assemblyFiles)
    {
        this.assemblyFiles = assemblyFiles;
    }

    protected override void Load(ContainerBuilder builder)
    {
        if (!ModInformation.IsNavalEnabled) return;

        var loader = new NavalAssemblyLoader(Path.GetDirectoryName(typeof(GameInterfaceModule).Assembly.Location));
        foreach (string file in assemblyFiles)
        {
            if (loader.TryLoad(file, out var assembly)) builder.RegisterAssemblyModules(assembly);
        }
    }
}
