using GameInterface.Policies;

namespace E2E.Tests.Environment;
internal class TestPolicy : ISyncPolicy
{
    public bool AllowOriginals { get; set; }

    public bool AllowOriginal() => AllowOriginals;
}
