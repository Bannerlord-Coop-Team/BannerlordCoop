using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Registry.Auto;

public class AutoRegistryFactoryTests
{
    [Fact]
    public void OrderByRegistrationPriority_ReturnsHigherPriorityCallbacksFirst()
    {
        var calls = new List<string>();
        var registrations = new (int Priority, Action Callback)[]
        {
            (0, () => calls.Add("standard")),
            (100, () => calls.Add("parent")),
        };

        foreach (var callback in AutoRegistryFactory.OrderByRegistrationPriority(registrations))
        {
            callback();
        }

        Assert.Equal(new[] { "parent", "standard" }, calls);
    }
}
