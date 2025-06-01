using Common;
using Common.Logging;
using Common.Tests.Utils;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using GameInterface.Tests.Bootstrap;
using GameInterface.Utils;
using HarmonyLib;
using Moq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

/// <summary>
/// Testing environment for End to End testing
/// </summary>
internal class E2ETestEnvironment : IDisposable 
{
    public IEnumerable<EnvironmentInstance> Clients => IntegrationEnvironment.Clients;
    public EnvironmentInstance Server => IntegrationEnvironment.Server;

    private TestEnvironment IntegrationEnvironment { get; }
    
    private Action<string> TestOutputCallback { get; }


    private Dictionary<Type, List<string>> StringIdListMappings = new();

    private DynamicSyncRegistry dynamicSyncRegistry;

    public E2ETestEnvironment(ITestOutputHelper output, int numClients = 2)
    {
        TestOutputCallback = (logMessage) => output.WriteLine(logMessage);


        // Setup logging for tests
        OutputSinkManager.AddLogCallback(TestOutputCallback);

        GameLoopRunner.Instance.SetGameLoopThread();

        GameBootStrap.Initialize();


        IntegrationEnvironment = new TestEnvironment(output, numClients, registerGameInterface: true);

        // Needs to be before patching
        SetupAutoSync();

        SetupMainHero();

        Server.Resolve<TestMessageBroker>().SetStaticInstance();
        Server.Resolve<IGameInterface>().PatchAll();
        SetupDynamicSync();

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            Server.ObjectManager.AddExisting(settlement.StringId, settlement);
        }

    }

    public void Dispose()
    {
        Server.Dispose();

        foreach (var client in Clients)
        {
            client.Dispose();
        }

        OutputSinkManager.RemoveLogCallback(TestOutputCallback);
        ContainerProvider.Clear();
    }

    private void SetupAutoSync()
    {
        Server.Resolve<IAutoSyncBuilder>().Build();

        foreach (var client in Clients)
        {
            client.Resolve<IAutoSyncBuilder>().Build();
        }
    }
    private void SetupDynamicSync()
    {
        var serverPatcher = Server.Resolve<DynamicSyncPatcher>();
        // Required as Harmony patches are not rebound per test so we need to explicitly rebind only the handlers
        serverPatcher.BindHandlers(DynamicSyncPatcher.Assembly);
        foreach (var client in Clients)
        {
            client.Resolve<DynamicSyncPatcher>().BindHandlers(DynamicSyncPatcher.Assembly);
        }
    }

    private void SetupMainHero()
    {
        // Setup main hero
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
                var mainHero = HeroCreator.CreateSpecialHero(characterObject);
                characterObject.HeroObject = mainHero;
                Game.Current.PlayerTroop = characterObject;
            }
        });


        foreach(var client in Clients)
        {
            client.Call(() =>
            {
                using (new AllowedThread())
                {
                    var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
                    var mainHero = HeroCreator.CreateSpecialHero(characterObject);
                    characterObject.HeroObject = mainHero;
                    Game.Current.PlayerTroop = characterObject;
                }
            });
        }
    }

    private void AddToStringIdLists<T>(string instanceId) where T : class
    {
        if (StringIdListMappings.ContainsKey(typeof(T)))
            StringIdListMappings[typeof(T)].Add(instanceId);
        else
            StringIdListMappings.Add(typeof(T), new List<string> { instanceId });
    }

    /// <summary>
    /// Creates a new object of type <typeparamref name="T"/> that is registered on the server and all clients.
    /// </summary>
    /// <remarks>
    /// This uses the <see cref="GameObjectCreator"/> to generate objects.
    /// </remarks>
    /// <typeparam name="T">Type of object to create</typeparam>
    /// <param name="disabledMethods">Methods to disable while calling this method</param>
    /// <returns>New object of type <typeparamref name="T"/></returns>
    /// <exception cref="Exception">Failed to create object exception</exception>
    public string CreateRegisteredObject<T>(IEnumerable<MethodBase>? disabledMethods = null) where T : class
    {
        string? id = null;
        Server.Call(() =>
        {
            var obj = GameObjectCreator.CreateInitializedObject<T>();

            if (Server.ObjectManager.TryGetId(obj, out id) == false)
            {
                throw new Exception($"Server object manager failed to register new object {typeof(T).Name}");
            }
        }, disabledMethods);

        if (id == null)
        {
            throw new Exception($"Failed to create {typeof(T).Name} on Server");
        }

        AddToStringIdLists<T>(id);
        return id;
    }

    /// <summary>
    /// Gets the field changed intercept from the given <paramref name="field"/>
    /// </summary>
    /// <param name="field">Field to get intercept from</param>
    /// <returns>Field intercept as <see cref="MethodInfo"/></returns>
    public MethodInfo GetIntercept(FieldInfo field)
    {
        Assert.True(GenericPatchHelpers.FieldInterceptCache.TryGetValue(field, out var intercept));
        return intercept;
    }

    /// <summary>
    /// Gets the collection add intercept from the given <paramref name="member"/>
    /// </summary>
    /// <param name="member">Member to get intercept from</param>
    /// <returns>Collection add intercept as <see cref="MethodInfo"/></returns>
    public MethodInfo GetCollectionAddIntercept(MemberInfo member)
    {

        Assert.True(GenericPatchHelpers.CollectionAddInterceptCache.TryGetValue(member, out var intercept));
        return intercept;
    }

    /// <summary>
    /// Gets the collection remove intercept from the given <paramref name="member"/>
    /// </summary>
    /// <param name="member">Member to get intercept from</param>
    /// <returns>Collection remove intercept as <see cref="MethodInfo"/></returns>
    public MethodInfo GetCollectionRemoveIntercept(MemberInfo member)
    {

        Assert.True(GenericPatchHelpers.CollectionRemoveInterceptCache.TryGetValue(member, out var intercept));
        return intercept;
    }

    /// <summary>
    /// Gets the array change intercept from the given <paramref name="member"/>
    /// </summary>
    /// <param name="member">Member to get intercept from</param>
    /// <returns>Array change intercept as <see cref="MethodInfo"/></returns>
    public MethodInfo GetArrayChangeIntercept(MemberInfo member)
    {

        Assert.True(GenericPatchHelpers.ArrayChangeInterceptCache.TryGetValue(member, out var intercept));
        return intercept;
    }

    /// <summary>
    /// Assert if the given field with a ValueType is properly synced between server and clients
    /// </summary>
    /// <typeparam name="TInstance">Type of instance that is tested</typeparam>
    /// <typeparam name="TField">Type of the field that is tested</typeparam>
    /// <param name="fieldName">Name of the field to be verified</param>
    /// <param name="value">Value to use for assertions has to be of type <typeparamref name="TField"/></param>
    /// <param name="instanceStringId">The specific stringId of the instance to be tested defaults to the first registered instance <typeparamref name="TInstance"/></param>
    public void AssertField<TInstance, TField>(string fieldName, TField value, string instanceStringId = null, TField? defaultValue = default(TField))
        where TInstance : class
    {
        bool isTextObject = typeof(TField) == typeof(TextObject);
        Assert.True(typeof(TField).IsValueType || typeof(TField) == typeof(string) || isTextObject);
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        var intercept = GetIntercept(fieldInfo);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            if (isTextObject && (defaultValue ?? fieldInfo.GetUnderlyingType().GetDefaultValue()) is TextObject)
                Assert.Equal(((defaultValue ?? fieldInfo.GetUnderlyingType().GetDefaultValue()) as TextObject)?.Value, (fieldInfo.GetValue(serverInstance) as TextObject)?.Value);
            else
                Assert.Equal(defaultValue ?? fieldInfo.GetUnderlyingType().GetDefaultValue(), fieldInfo.GetValue(serverInstance));

            intercept.Invoke(null, new object[] { serverInstance, value, fieldName });
            Assert.True(value.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {value} Actual: {fieldInfo.GetValue(serverInstance)}");
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            if (isTextObject && value is TextObject)
                Assert.True((value as TextObject).Equals(fieldInfo.GetValue(clientInstance) as TextObject), $"Expected: {value} Actual: {fieldInfo.GetValue(clientInstance)}");
            else
                Assert.True(value.Equals(fieldInfo.GetValue(clientInstance)), $"Expected: {value} Actual: {fieldInfo.GetValue(clientInstance)}");
        }
    }

    /// <summary>
    /// Assert if the given field with a ReferenceType is properly synced between server and clients
    /// </summary>
    /// <typeparam name="TInstance">Type of instance that is tested</typeparam>
    /// <typeparam name="TField">Type of the field that is tested</typeparam>
    /// <param name="fieldName">Name of the field to be verified</param>
    /// <param name="value">Value to use for assertions has to be of type <typeparamref name="TField"/></param>
    /// <param name="instanceStringId">The specific stringId of the instance to be tested defaults to the first registered instance <typeparamref name="TField"/></param>
    /// <param name="referenceStringId">The specific stringId of the referenced object to be tested defaults to the first registered instance <typeparamref name="TInstance"/></param>
    public void AssertReferenceField<TInstance, TField>(string fieldName, string? instanceStringId = null, string? referenceStringId = null, TField? defaultValue = null)
        where TInstance : class
        where TField : class
    {
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        Assert.False(fieldInfo.GetUnderlyingType().IsValueType);
        var intercept = GetIntercept(fieldInfo);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];
        string referenceId = referenceStringId ?? StringIdListMappings[typeof(TField)][0];
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(referenceId, out var serverFieldInstance));
            Assert.Equal(defaultValue ?? fieldInfo.GetUnderlyingType().GetDefaultValue(), fieldInfo.GetValue(serverInstance));
            intercept.Invoke(null, new object[] { serverInstance, serverFieldInstance, fieldName });
            Assert.True(serverFieldInstance.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {serverFieldInstance} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.NotNull(serverFieldInstance);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            Assert.True(client.ObjectManager.TryGetObject<TField>(referenceId, out var clientFieldInstance));
            Assert.True(clientFieldInstance.Equals(fieldInfo.GetValue(clientInstance)), $"Expected: {clientFieldInstance} Actual: {fieldInfo.GetValue(clientInstance)}");
            Assert.NotNull(clientFieldInstance);
        }
    }

    public void AssertCollectionReferenceField<TInstance, TField>(string fieldName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();

        var setIntercept = GetIntercept(fieldInfo);
        var addIntercept = GetCollectionAddIntercept(fieldInfo);
        var removeIntercept = GetCollectionRemoveIntercept(fieldInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));

        var initialValues = new List<TField>
        {
            valueInstance
        };

        var collection = (IEnumerable<TField>)Activator.CreateInstance(fieldInfo.FieldType, initialValues);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            setIntercept.Invoke(null, new object[] { serverInstance, collection, fieldName });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)fieldInfo.GetValue(clientInstance);
            
            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
        

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            addIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out var serverValue));
            removeIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }

    public void AssertCollectionReferenceProperty<TInstance, TField>(string propertyName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();

        var addIntercept = GetCollectionAddIntercept(propertyInfo);
        var removeIntercept = GetCollectionRemoveIntercept(propertyInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));

        var initialValues = new List<TField>
        {
            valueInstance
        };

        var collection = (IEnumerable<TField>)Activator.CreateInstance(propertyInfo.PropertyType, initialValues);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            propertyInfo.SetValue(serverInstance, collection);

            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }


        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            addIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out var serverValue));
            removeIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (IEnumerable<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }

    public void AssertQueueReferenceField<TInstance, TField>(string fieldName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();

        var setIntercept = GetIntercept(fieldInfo);
        var addIntercept = GetCollectionAddIntercept(fieldInfo);
        var removeIntercept = GetCollectionRemoveIntercept(fieldInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));

        var initialValues = new List<TField>
        {
            valueInstance
        };

        var collection = (Queue<TField>)Activator.CreateInstance(fieldInfo.FieldType, initialValues);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            setIntercept.Invoke(null, new object[] { serverInstance, collection, fieldName });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            addIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out var serverValue));
            removeIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }


    public void AssertQueueReferenceProperty<TInstance, TField>(string propertyName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();

        var addIntercept = GetCollectionAddIntercept(propertyInfo);
        var removeIntercept = GetCollectionRemoveIntercept(propertyInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));

        var initialValues = new List<TField>
        {
            valueInstance
        };

        var collection = (Queue<TField>)Activator.CreateInstance(propertyInfo.PropertyType, initialValues);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            propertyInfo.SetValue(serverInstance, collection);
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            addIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), serverValue, serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out var serverValue));
            removeIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (Queue<TField>)propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }

    public void AssertArrayReferenceField<TInstance, TField>(string fieldName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var fieldInfo = AccessTools.Field(typeof(TInstance), fieldName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();

        var setIntercept = GetIntercept(fieldInfo);
        var changeIntercept = GetArrayChangeIntercept(fieldInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));
        var collection = new TField[] { null, null, valueInstance, null, null }; 
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            setIntercept.Invoke(null, new object[] { serverInstance, collection, fieldName });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(1, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            changeIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), 1, serverValue, serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            changeIntercept.Invoke(null, new object[] { fieldInfo.GetValue(serverInstance), 2, null, serverInstance });
            Assert.True(collection.Equals(fieldInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {fieldInfo.GetValue(serverInstance)}");
            Assert.Equal(2, collection.Count());
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])fieldInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                Assert.Equal(serverReferenceId, clientReferenceId);
            }
        }
    }

    public void AssertArrayReferenceProperty<TInstance, TField>(string propertyName, string? instanceStringId = null)
        where TInstance : class
        where TField : class
    {
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];

        string firstReferenceId = CreateRegisteredObject<TField>();
        string secondReferenceId = CreateRegisteredObject<TField>();
        var changeIntercept = GetArrayChangeIntercept(propertyInfo);

        Assert.True(Server.ObjectManager.TryGetObject<TField>(firstReferenceId, out TField valueInstance));
        var collection = new TField[] { null, null, valueInstance, null, null };
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            propertyInfo.SetValue(serverInstance, collection);
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                if(collection.ElementAt(i) == null)
                {
                    Assert.Equal(collection.ElementAt(i), clientList.ElementAt(i));
                }
                else 
                { 
                    Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                    Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                    Assert.Equal(serverReferenceId, clientReferenceId);
                }
            }
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            changeIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), 1, serverValue, serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                if (collection.ElementAt(i) == null)
                {
                    Assert.Equal(collection.ElementAt(i), clientList.ElementAt(i));
                }
                else
                {
                    Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                    Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                    Assert.Equal(serverReferenceId, clientReferenceId);
                }
            }
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TField>(secondReferenceId, out var serverValue));
            changeIntercept.Invoke(null, new object[] { propertyInfo.GetValue(serverInstance), 2, null, serverInstance });
            Assert.True(collection.Equals(propertyInfo.GetValue(serverInstance)), $"Expected: {collection} Actual: {propertyInfo.GetValue(serverInstance)}");
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            var clientList = (TField[])propertyInfo.GetValue(clientInstance);

            Assert.Equal(collection.Count(), clientList.Count());
            for (int i = 0; i < clientList.Count(); i++)
            {
                if (collection.ElementAt(i) == null)
                {
                    Assert.Equal(collection.ElementAt(i), clientList.ElementAt(i));
                }
                else
                {
                    Assert.True(Server.ObjectManager.TryGetId(collection.ElementAt(i), out string serverReferenceId));
                    Assert.True(client.ObjectManager.TryGetId(clientList.ElementAt(i), out string clientReferenceId));
                    Assert.Equal(serverReferenceId, clientReferenceId);
                }
            }
        }
    }

    /// <summary>
    /// Assert if the given property with a ValueType is properly synced between server and clients
    /// </summary>
    /// <typeparam name="TInstance">Type of instance that is tested</typeparam>
    /// <typeparam name="TProperty">Type of the property that is tested</typeparam>
    /// <param name="propertyName">Name of the property to be verified</param>
    /// <param name="value">Value to use for assertions has to be of type <typeparamref name="TProperty"/></param>
    /// <param name="defaultValue">Defaultvalue of the property if its preinitialized by Taleworlds. Has to be of type <typeparamref name="TProperty"/></param>
    /// <param name="instanceStringId">The specific stringId of the instance to be tested defaults to the first registered instance <typeparamref name="TInstance"/></param>
    public void AssertProperty<TInstance, TProperty>(string propertyName, TProperty value, TProperty? defaultValue = default, string? instanceStringId = null)
        where TInstance : class
    {
        bool isTextObject = typeof(TProperty) == typeof(TextObject);
        Assert.True(typeof(TProperty).IsValueType || typeof(TProperty) == typeof(string) || typeof(TProperty) == typeof(TextObject));
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];
        Server.Call((Action)(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            if (isTextObject && (defaultValue ?? propertyInfo.GetUnderlyingType().GetDefaultValue()) is TextObject)
                Assert.Equal(((defaultValue ?? propertyInfo.GetUnderlyingType().GetDefaultValue()) as TextObject)?.Value, (propertyInfo.GetValue(serverInstance) as TextObject)?.Value);
            else
                Assert.Equal(defaultValue ?? propertyInfo.GetUnderlyingType().GetDefaultValue(), propertyInfo.GetValue(serverInstance));
            propertyInfo.SetValue(serverInstance,value);
        }));

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            Assert.Equal(value, propertyInfo.GetValue(clientInstance));
        }
    }

    /// <summary>
    /// Assert if the given property with a ReferenceType is properly synced between server and clients
    /// </summary>
    /// <typeparam name="TInstance">Type of instance that is tested</typeparam>
    /// <typeparam name="TProperty">Type of the field that is tested</typeparam>
    /// <param name="fieldName">Name of the field to be verified</param>
    /// <param name="value">Value to use for assertions has to be of type <typeparamref name="TProperty"/></param>
    /// <param name="instanceStringId">The specific stringId of the instance to be tested defaults to the first registered instance <typeparamref name="TInstance"/></param>
    /// <param name="referenceStringId">The specific stringId of the referenced object to be tested defaults to the first registered instance <typeparamref name="TProperty"/></param>
    public void AssertReferenceProperty<TInstance, TProperty>(string propertyName, string? instanceStringId = null, string? referenceStringId = null)
        where TInstance : class
        where TProperty : class
    {
        var propertyInfo = AccessTools.Property(typeof(TInstance), propertyName);
        string instanceId = instanceStringId ?? StringIdListMappings[typeof(TInstance)][0];
        string referenceId = referenceStringId ?? StringIdListMappings[typeof(TProperty)][0];

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<TInstance>(instanceId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<TProperty>(referenceId, out var serverPropertyInstance));
            Assert.Equal(propertyInfo.GetUnderlyingType().GetDefaultValue(), propertyInfo.GetValue(serverInstance));
            propertyInfo.SetValue(serverInstance, serverPropertyInstance);
            Assert.NotNull(serverInstance);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TInstance>(instanceId, out var clientInstance));
            Assert.True(client.ObjectManager.TryGetObject<TProperty>(referenceId, out var clientPropertyInstance));
            Assert.Same(clientPropertyInstance, propertyInfo.GetValue(clientInstance));
            Assert.NotNull(clientPropertyInstance);
        }
    }
}