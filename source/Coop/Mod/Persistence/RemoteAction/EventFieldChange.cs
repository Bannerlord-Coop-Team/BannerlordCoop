using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NLog;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RemoteAction;
using Sync.Behaviour;

namespace Coop.Mod.Persistence.RemoteAction
{
    public class EventFieldChange : EventActionBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [OnlyIn(Component.Client)] [CanBeNull] private readonly IEnvironmentClient m_EnvironmentClient;

        [OnlyIn(Component.Server)] [CanBeNull] private readonly IEnvironmentServer m_EnvironmentServer;

        [OnlyIn(Component.Client)]
        public EventFieldChange([NotNull] IEnvironmentClient environment)
        {
            m_EnvironmentClient = environment;
        }

        [OnlyIn(Component.Server)]
        public EventFieldChange([NotNull] IEnvironmentServer environment)
        {
            m_EnvironmentServer = environment;
        }

        [EventData] public FieldChange Field { get; set; }

        public override IEnumerable<Argument> Arguments => Field.Arguments;
        public override IEnumerable<EntityId> AffectedEntities => null;

        public override bool IsValid()
        {
            return Field.IsValid();
        }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (Sync.Registry.IdToField.TryGetValue(Field.Id, out var field))
            {
                if (room is RailServerRoom serverRoom)
                {
                    if (!IsValid())
                    {
                        ActionValidator.TryGet(Field.Id, out IActionValidator validator);
                        Logger.Info("[{EventId}] Broadcast FieldChange '{Field}' rejected by {Validator}: {Reason}", EventId, Field, validator.GetType().Name, validator.GetReasonForRejection());
                        return;
                    }

                    Logger.Trace("[{EventId}] Broadcast FieldChange: {Field}", EventId, Field);
                    m_EnvironmentServer.EventQueue.Add(serverRoom, this);
                }
                else if (room is RailClientRoom)
                {
                    Logger.Trace("[{EventId}] FieldChange: {Field}", EventId, Field);
                    // TODO: The call is not synchronized to a campaign time at this point. We probably want an execution queue of some sorts that executes the call at the right point in time.
                    field.Set(
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Field.Instance),
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Field.Arguments.First()));
                }
            }
            else
            {
                Logger.Warn("[{EventId}] Unknown FieldChange: {Field}", EventId, Field);
            }
        }
    }
}