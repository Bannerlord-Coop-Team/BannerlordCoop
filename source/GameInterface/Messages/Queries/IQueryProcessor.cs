using Common.Messages.Queries;

namespace GameInterface.Messages.Queries
{
    public interface IQueryProcessor<in TQuery, T> where TQuery: IQuery<T>
    {
        void Initialize(IGameInterface gameInterface);

        T Process(TQuery query);
    }
}
