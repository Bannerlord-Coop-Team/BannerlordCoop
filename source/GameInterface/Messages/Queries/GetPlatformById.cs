using Common.Messages.Queries;

namespace GameInterface.Messages.Queries
{
    public class GetPlatformById : IQuery<string>
    {
        public string Id { get; set; }
    }
}
