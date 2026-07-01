using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Tests.Mocks;
using GameInterface.AutoSync;
using GameInterface.CoopSessionData;
using GameInterface.Registry;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Time.Interfaces;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Interfaces;
using Moq;
using Serilog;
using System;
using Xunit.Abstractions;
using IGameInterface = GameInterface.IGameInterface;

namespace Coop.Tests;

internal abstract class TestComponentBase
{
    public ITestOutputHelper Output { get; }

    public TestMessageBroker TestMessageBroker { get; protected set; }
    public TestNetwork TestNetwork { get; protected set; }
    public IContainer Container { get; protected set; }

    protected TestComponentBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Builds container and assigns class properties for ease of use in testing
    /// </summary>
    /// <param name="builder">Container builder</param>
    /// <returns>Container with all common types registered</returns>
    protected IContainer BuildContainer(ContainerBuilder builder)
    {
        RegisterCommonTypes(builder);

        var container = builder.Build();

        TestMessageBroker = container.Resolve<TestMessageBroker>();
        TestNetwork = container.Resolve<TestNetwork>();
        Container = container;

        TestMessageBroker.Messages.Clear();
        TestNetwork.Clear();

        return container;
    }

    private ContainerBuilder RegisterCommonTypes(ContainerBuilder builder)
    {
        builder.RegisterType<SerializableTypeMapper>().As<ISerializableTypeMapper>().InstancePerLifetimeScope();
        builder.RegisterType<ProtoBufSerializer>().As<ICommonSerializer>().InstancePerLifetimeScope();
        builder.RegisterType<TestMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
        builder.RegisterType<TestNetwork>().AsSelf().As<INetwork>().InstancePerLifetimeScope();
        builder.RegisterType<ModuleValidator>().As<IModuleValidator>().SingleInstance();


        builder.RegisterType<ObjectManager>().As<IObjectManager>().InstancePerLifetimeScope();
        builder.RegisterType<RegistryCollection>().As<IRegistryCollection>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomCreationSettlementTracker>().As<IKingdomCreationSettlementTracker>().InstancePerLifetimeScope();
        builder.RegisterType<KingdomDecisionDataConverter>().As<IKingdomDecisionDataConverter>().InstancePerLifetimeScope();

        RegisterMock<ILogger>(builder);
        RegisterMock<IGameInterface>(builder);
        RegisterMock<IAutoSyncPatchCollector>(builder);
        RegisterMock<IHeroInterface>(builder);
        RegisterMock<IModuleInfoProvider>(builder);
        RegisterMock<IRegistryManager>(builder);
        RegisterPlayerManagerMock(builder);
        RegisterMock<ITimeControlInterface>(builder);
        RegisterMock<ITroopRosterInterface>(builder);
        RegisterMock<IMapTimeTrackerInterface>(builder);
        RegisterMock<ILoadingInterface>(builder);
        RegisterMock<ICoopSessionProvider>(builder);
        RegisterMock<ITroopRosterInterface>(builder);
        RegisterMock<IMobilePartyInterface>(builder);
        RegisterMock<IGameStateInterface>(builder);
        RegisterMock<ISettlementInterface>(builder);
        RegisterMock<IBattleTroopReserveBuilder>(builder);

        // ISaveInterface is consumed by TransferSaveState's constructor, which packages a save the
        // moment the state is entered. Give it a non-null default so simply entering the state does
        // not NRE; tests that assert on the transferred save re-stub the return value.
        var saveInterfaceMock = new Mock<ISaveInterface>();
        saveInterfaceMock.Setup(m => m.SaveCurrentGame())
            .Returns(new SaveResults(true, Array.Empty<byte>(), "test-campaign"));
        builder.RegisterInstance(saveInterfaceMock).AsSelf().SingleInstance();
        builder.RegisterInstance(saveInterfaceMock.Object).As<ISaveInterface>().SingleInstance();

        return builder;
    }

    protected void RegisterMock<T>(ContainerBuilder builder) where T : class
    {
        var mock = new Mock<T>();
        builder.RegisterInstance(mock).AsSelf().SingleInstance();
        builder.RegisterInstance(mock.Object).As<T>().SingleInstance();
    }

    /// <summary>
    /// The connection states replay the existing players to a joining peer, iterating
    /// <see cref="IPlayerManager.Players"/>. Default it to empty so simply entering those states does not
    /// NRE; tests that exercise the replay re-stub it.
    /// </summary>
    private void RegisterPlayerManagerMock(ContainerBuilder builder)
    {
        var mock = new Mock<IPlayerManager>();
        mock.Setup(m => m.Players).Returns(Array.Empty<Player>());
        builder.RegisterInstance(mock).AsSelf().SingleInstance();
        builder.RegisterInstance(mock.Object).As<IPlayerManager>().SingleInstance();
    }

    private IContainer SetupContainerProvider(ContainerBuilder builder)
    {
        var container = builder.Build();

        global::GameInterface.ContainerProvider.SetContainer(container);

        return container;
    }
}
