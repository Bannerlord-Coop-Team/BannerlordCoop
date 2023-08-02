﻿using Autofac;
using Common;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Server;
using Coop.Core.Surrogates;
using GameInterface;
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
        }

        ~CoopartiveMultiplayerExperience()
        {
            harmony.UnpatchAll(HarmonyId);
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
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ServerModule>();
            _container = builder.Build();

            updateable = _container.Resolve<INetwork>();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ClientModule>();
            _container = builder.Build();

            updateable = _container.Resolve<INetwork>();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }
    }
}
