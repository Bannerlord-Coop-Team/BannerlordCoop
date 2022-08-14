using Common.Messages.Queries;

namespace GameInterface.Messages.Queries
{
    public abstract class QueryProcessor<TQuery, T> : IQueryProcessor<TQuery, T> where TQuery : IQuery<T>
    {
        public void Initialize(IGameInterface gameInterface)
        {
            GameInterface = gameInterface;
        }

        public abstract T Process(TQuery query);

        public IGameInterface GameInterface;
    }
}
