using E2E.Tests.Environment;
using GameInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace E2E.Tests;
public class LifetimeProtoTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public LifetimeProtoTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Patch_Test()
    {
        // Arrange
        // Act
        string? testObjId = null;
        TestEnvironment.Server.Call(() =>
        {
            var testObj = new TestObject();

            Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(testObj, out testObjId));
        });

        Assert.NotNull(testObjId);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TestObject>(testObjId, out var _));
        }
    }
}
