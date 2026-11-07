using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Moq;
using System;
using System.Linq;

namespace GameInterface.Tests.Utils.Commands;

// Builds command instances for metadata and argument-validation tests, filling constructor dependencies.
internal static class CommandTestFactory
{
    // Creates the command through its widest public constructor: the ambient (or a fresh) ObjectManager, a real resolver, Moq mocks for other interfaces.
    public static T Create<T>(Type type)
    {
        var constructor = type.GetConstructors()
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var objectManager = AmbientObjectManager() ?? new ObjectManager(Serilog.Core.Logger.None);
        var arguments = constructor.GetParameters()
            .Select(parameter => CreateDependency(parameter.ParameterType, objectManager))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }

    // The IObjectManager a test installed through ContainerProvider, if any.
    private static IObjectManager AmbientObjectManager()
    {
        if (!ContainerProvider.TryGetContainer(out var container)) return null;

        return container.TryResolve<IObjectManager>(out var objectManager) ? objectManager : null;
    }

    // Supplies one constructor dependency, or null when the type can't be faked.
    private static object CreateDependency(Type type, IObjectManager objectManager)
    {
        if (type == typeof(IObjectManager)) return objectManager;
        if (type == typeof(ICommandObjectResolver)) return new CommandObjectResolver(objectManager);
        if (!type.IsInterface) return null;

        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type));
        return mock.Object;
    }
}
