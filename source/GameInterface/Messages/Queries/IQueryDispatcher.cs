using Common.Messages.Queries;

namespace GameInterface.Messages.Queries
{
    public interface IQueryDispatcher
    {
        T Dispatch<T>(IQuery<T> query);
    }
}
