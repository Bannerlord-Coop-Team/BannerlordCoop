using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

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
            var issue = IssueManager.GetIssueOfQuest(quest);
            if (issue == null || !registry.IsRegistered(issue.GetType()))
            {
                return;
            }

            // Which quests have the listener is only known at runtime, so it is looked up by its vanilla name
            var listener = Listeners.GetOrAdd((quest.GetType(), listenerName), key => key.Quest.GetMethod(
                key.Listener,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null));

            listener?.Invoke(quest, arguments);
        });
    }
}
