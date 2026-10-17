using System;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IQuestTypeRegistry
{
    IReadOnlyCollection<IQuestTypeDescriptor> Descriptors { get; }

    bool IsRegistered(Type issueType);

    bool TryGet(Type issueType, out IQuestTypeDescriptor descriptor);

    // Mainly for commands. If you are using this for something else check yoself.
    bool TryGetByName(string issueTypeName, out IQuestTypeDescriptor descriptor);
}
