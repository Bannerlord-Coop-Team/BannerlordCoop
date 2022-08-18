using Autofac;
using Common.Messaging;
using System.Linq;

namespace GameInterface.Services.GameState
{
    internal class GameStateModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var assembly = GetType().Assembly;
            var @namespace = GetType().Namespace;
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IHandler)) != null 
                && t.GetType().Namespace.StartsWith(@namespace));
            foreach (var type in types)
            {
                builder.RegisterType(type).As<IHandler>().SingleInstance();
            }
            base.Load(builder);
        }
    }
}
