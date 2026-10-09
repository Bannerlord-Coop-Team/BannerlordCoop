using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Issues.Framework.Registries;

internal class QuestTypeRegistry : IQuestTypeRegistry
{
    private readonly Dictionary<Type, IQuestTypeDescriptor> descriptorsByType = new();
    private readonly Dictionary<string, IQuestTypeDescriptor> descriptorsByName = new();

    public IReadOnlyCollection<IQuestTypeDescriptor> Descriptors { get; }

    public QuestTypeRegistry(IEnumerable<IQuestTypeDescriptor> descriptors)
    {
        Descriptors = descriptors.ToList();

        foreach (var descriptor in Descriptors)
        {
            if (descriptorsByType.ContainsKey(descriptor.IssueType))
            {
                throw new InvalidOperationException($"{descriptor.IssueType.Name} is registered by more than one descriptor");
            }

            descriptorsByType.Add(descriptor.IssueType, descriptor);
            descriptorsByName.Add(descriptor.IssueType.Name, descriptor);
        }
    }

    public bool IsRegistered(Type issueType) => descriptorsByType.ContainsKey(issueType);

    public bool TryGet(Type issueType, out IQuestTypeDescriptor descriptor)
    {
        return descriptorsByType.TryGetValue(issueType, out descriptor);
    }

    public bool TryGetByName(string issueTypeName, out IQuestTypeDescriptor descriptor)
    {
        descriptor = null;
        return issueTypeName != null && descriptorsByName.TryGetValue(issueTypeName, out descriptor);
    }
}
