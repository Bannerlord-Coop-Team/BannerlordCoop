using Xunit;

namespace Coop.Tests;

/// <summary>
/// Serializes tests that mutate the process-wide server/client role.
/// </summary>
[CollectionDefinition(nameof(ModInformationRoleCollection), DisableParallelization = true)]
public class ModInformationRoleCollection
{
    public const string Name = nameof(ModInformationRoleCollection);
}
