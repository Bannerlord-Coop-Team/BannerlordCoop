using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;
public class SyncTownTests : IDisposable
{
	E2ETestEnvironment TestEnvironment { get; }

	EnvironmentInstance Server => TestEnvironment.Server;

	IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

	private Dictionary<Type, string> StringIdDict = new();

	public SyncTownTests(ITestOutputHelper output)
	{
		TestEnvironment = new E2ETestEnvironment(output);

		CreateObject<Town>();
		CreateObject<Hero>();
	}


    [Fact]
    public void Server_Town_Fields()
    {
        AssertField<Town, float>(nameof(Town._prosperity), 500f);

        AssertField<Town, int>(nameof(Town._tradeTax), 70);
        AssertField<Town, int>(nameof(Town._wallLevel), 1);
        AssertField<Town, int>(nameof(Town.BoostBuildingProcess), 200);
        AssertField<Town, bool>(nameof(Town._isCastle), true);
        AssertField<Town, bool>(nameof(Town.InRebelliousState), true);

        AssertReferenceField<Town, Hero>(nameof(Town._governor));
    }

    [Fact]
    public void Server_Town_Properties()
    {
        AssertProperty<Town, float>(nameof(Town.Security), 50f);
        AssertProperty<Town, float>(nameof(Town.Loyalty), 60f);

        AssertReferenceProperty<Town, Hero>(nameof(Town.Governor));
    }

    public void Dispose()
	{
		TestEnvironment.Dispose();
	}

	private void CreateObject<T>() where T: class
	{
		string instanceId = string.Empty;
		T serverInstance = null;
		Server.Call(() =>
		{
            serverInstance = GameObjectCreator.CreateInitializedObject<T>();
            Assert.True(Server.ObjectManager.TryGetId(serverInstance, out instanceId));
		});

		// Create town on all clients
		foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<T>(instanceId, out T clientInstance));
			Assert.NotSame(serverInstance, clientInstance);
		}

		StringIdDict.Add(typeof(T), instanceId);
	}


	private void AssertField<TInstance, TValue>(string fieldName, TValue value)
        where TInstance : class
    {
        Assert.True(typeof(TValue).IsValueType);
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
		var intercept = TestEnvironment.GetIntercept(fieldInfo);

        Server.Call(() =>
		{
			Assert.True(Server.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var serverInstance));

            Assert.Equal(fieldInfo.GetUnderlyingType().GetDefaultValue(), fieldInfo.GetValue(serverInstance));
            intercept.Invoke(null, new object[] { serverInstance, value});
            Assert.Equal(value, fieldInfo.GetValue(serverInstance));
		});

		// Assert
		foreach (var client in Clients)
		{
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var clientInstance));
			Assert.Equal(value, fieldInfo.GetValue(clientInstance));
		}
	}

    private void AssertReferenceField<TInstance, TValue>(string fieldName) 
        where TInstance : class
        where TValue : class
    {
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        Assert.False(fieldInfo.GetUnderlyingType().IsValueType);
        var intercept = TestEnvironment.GetIntercept(fieldInfo);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TValue>(StringIdDict[typeof(TValue)], out var serverFieldInstance));
            Assert.Equal(fieldInfo.GetUnderlyingType().GetDefaultValue(), fieldInfo.GetValue(serverInstance));
            intercept.Invoke(null, new object[] { serverInstance, serverFieldInstance });
            Assert.Same(serverFieldInstance, fieldInfo.GetValue(serverInstance));
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var clientInstance));
            Assert.True(client.ObjectManager.TryGetObject<TValue>(StringIdDict[typeof(TValue)], out var clientFieldInstance));
            Assert.Same(clientFieldInstance, fieldInfo.GetValue(clientInstance));
        }
    }

    private void AssertProperty<TInstance, TValue>(string propertyName, TValue value)
        where TInstance : class
    {
        Assert.True(typeof(TValue).IsValueType);
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);

        Server.Call((Action)(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var serverInstance));

            Assert.Equal(propertyInfo.GetUnderlyingType().GetDefaultValue(), propertyInfo.GetValue((object)serverInstance));
            propertyInfo.SetValue(serverInstance, value);
        }));

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var clientInstance));
            Assert.Equal(value, propertyInfo.GetValue(clientInstance));
        }
    }

    private void AssertReferenceProperty<TInstance, TProperty>(string propertyName)
        where TInstance : class
        where TProperty : class
    {
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TProperty>(StringIdDict[typeof(TProperty)], out var serverPropertyInstance));
            Assert.Equal(propertyInfo.GetUnderlyingType().GetDefaultValue(), propertyInfo.GetValue(serverInstance));
            propertyInfo.SetValue(serverInstance, serverPropertyInstance);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(StringIdDict[typeof(TInstance)], out var clientInstance));
            Assert.True(client.ObjectManager.TryGetObject<TProperty>(StringIdDict[typeof(TProperty)], out var clientPropertyInstance));
            Assert.Same(clientPropertyInstance, propertyInfo.GetValue(clientInstance));
        }
    }
}
