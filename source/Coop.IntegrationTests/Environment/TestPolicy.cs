using Common.Util;
using GameInterface.Policies;

namespace Coop.IntegrationTests.Environment;
internal class TestPolicy : ISyncPolicy
{
    public bool AllowOriginal() => false;
}
