using GameInterface.Policies;

namespace E2E.Tests.Environment;
internal class TestPolicy : ISyncPolicy
{
    public bool AllowOriginal() => false;
}
