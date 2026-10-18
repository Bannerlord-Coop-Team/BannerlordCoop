using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class IssuesLayoutTests
{
    private const string IssuesNamespace = "GameInterface.Services.Issues";
    private const string InterfaceNamespace = "GameInterface.Services.Issues.Framework.Interface";

    private static IEnumerable<Type> IssueTypes()
    {
        return typeof(GameInterface).Assembly.GetTypes()
            .Where(type => type.Namespace != null &&
                (type.Namespace == IssuesNamespace || type.Namespace.StartsWith(IssuesNamespace + ".")));
    }

    private static bool IsReadOnly(Type type)
    {
        return type.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
    }

    [Fact]
    public void Interfaces_AreDeclaredInTheFrameworkInterfaceNamespace()
    {
        var misplaced = IssueTypes()
            .Where(type => type.IsInterface && type.Namespace != InterfaceNamespace)
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void Messages_ArePublicReadonlyStructs()
    {
        var misshapen = IssueTypes()
            .Where(type => typeof(IMessage).IsAssignableFrom(type) && !type.IsInterface)
            .Where(type => !type.IsValueType || !type.IsPublic || !IsReadOnly(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misshapen);
    }

    [Fact]
    public void Commands_AreProtoContractsThatSkipTheConstructor()
    {
        var misshapen = IssueTypes()
            .Where(type => typeof(ICommand).IsAssignableFrom(type) && !type.IsInterface)
            .Where(type => type.GetCustomAttribute<ProtoContractAttribute>()?.SkipConstructor != true)
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misshapen);
    }
}
