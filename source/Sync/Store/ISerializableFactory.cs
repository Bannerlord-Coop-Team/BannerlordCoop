namespace Sync.Store
{
    public interface ISerializableFactory
    {
        /// <summary>
        ///     Wraps an object to make it serializable. Returns <paramref name="obj" /> if no wrapper is
        ///     necessary.
        /// </summary>
        /// <param name="obj">Object that may be wrapped.</param>
        object Wrap(object obj);

        /// <summary>
        ///     Unwraps an object from the serializer wrapper. Returns <paramref name="obj" /> if it is not
        ///     wrapped.
        /// </summary>
        /// <param name="obj">Object to unwrap.</param>
        /// <returns></returns>
        object Unwrap(object obj);
    }
}
