using Autofac;
using Common;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common.Configuration;
using Coop.Core.Server;
using Coop.Core.Surrogates;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;
using HarmonyLib;
using System;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IUpdateable
    {
        private const string HarmonyId = "com.TaleWorlds.MountAndBlade.Bannerlord.Coop";
        private readonly Harmony harmony = new Harmony(HarmonyId);

        public CoopartiveMultiplayerExperience()
        {
            harmony.PatchAll(typeof(GameInterface.GameInterface).Assembly);
            SurrogateCollection.AssignSurrogates();

            MessageBroker.Instance.Subscribe<ConnectWithIP>(Handle);
            MessageBroker.Instance.Subscribe<HostSave>(Handle);
        }

        ~CoopartiveMultiplayerExperience()
        {
            harmony.UnpatchAll(HarmonyId);

            MessageBroker.Instance.Unsubscribe<ConnectWithIP>(Handle);
            MessageBroker.Instance.Unsubscribe<HostSave>(Handle);
        }

        private void Handle(MessagePayload<ConnectWithIP> obj)
        {
            var connectMessage = obj.What;

            var config = new NetworkConfiguration()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            StartAsClient(config);
        }

        private void Handle(MessagePayload<HostSave> obj)
        {
            StartAsServer();

            MessageBroker.Instance.Publish(this, new LoadGame(obj.What.SaveName));
        }

        public static UpdateableList Updateables { get; } = new UpdateableList();

        private IContainer _container;

        private IUpdateable updateable
        {
            get { return _updateable; }
            set
            {
                if(_updateable != null) 
                {
                    Updateables.Remove(value);
                }
                _updateable = value;
                Updateables.Add(_updateable);
            }
        }

        private IUpdateable _updateable;

        public int Priority => 0;

        public void Update(TimeSpan deltaTime)
        {
            Updateables.UpdateAll(deltaTime);
        }

        public void StartAsServer()
        {
            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ServerModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();
            _container = builder.Build();

            containerProvider.SetProvider(_container);

            updateable = _container.Resolve<INetwork>();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient(INetworkConfiguration configuration = null)
        {
            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ClientModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();

            if (configuration != null)
            {
                builder.RegisterInstance(configuration).As<INetworkConfiguration>().SingleInstance();
            }

            _container = builder.Build();

            containerProvider.SetProvider(_container);

            updateable = _container.Resolve<INetwork>();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }
    }
}
