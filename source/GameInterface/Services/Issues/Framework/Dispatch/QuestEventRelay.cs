using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class QuestEventRelay : IQuestEventRelay
{
    private static readonly ConcurrentDictionary<(Type Quest, string Listener), MethodInfo> Listeners =
        new ConcurrentDictionary<(Type Quest, string Listener), MethodInfo>();

    private readonly IOutcomeDispatcher dispatcher;
    private readonly IQuestTypeRegistry registry;

    public QuestEventRelay(IOutcomeDispatcher dispatcher, IQuestTypeRegistry registry)
    {
        this.dispatcher = dispatcher;
        this.registry = registry;
    }

    public void Relay(string listenerName, Type[] parameterTypes, params object[] arguments)
    {
        dispatcher.RunAsOwners<QuestBase>(quest =>
        {
            var issue = quest.QuestGiver?.Issue;
            if (issue?.IssueQuest != quest || !registry.IsRegistered(issue.GetType()))
            {
                return;
            }

            // Which quests have the listener is only known at runtime, so it is looked up by its vanilla name.
            var key = (quest.GetType(), listenerName);
            if (!Listeners.TryGetValue(key, out var listener))
            {
                listener = FindListener(key.Item1, key.Item2, parameterTypes);
                if (listener != null)
                    Listeners.TryAdd(key, listener);
            }

            listener?.Invoke(quest, arguments);
        });
    }

    // GetMethod does not return a private method declared on a base type, so each type is asked for its own
    private static MethodInfo FindListener(Type questType, string listenerName, Type[] parameterTypes)
    {
        for (var type = questType; type != null && type != typeof(QuestBase); type = type.BaseType)
        {
            var listener = type.GetMethod(
                listenerName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                parameterTypes,
                null);

            if (listener != null)
            {
                return listener;
            }
        }

        return null;
    }
}
