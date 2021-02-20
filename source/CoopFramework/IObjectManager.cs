namespace CoopFramework
{
    /// <summary>
    ///     Interface for an observer of an objects lifetime.
    /// </summary>
    public interface IObjectLifetimeObserver
    {
        /// <summary>
        ///     Called after an instance of the object was registered with the object manager. The object may be in
        ///     an uninitialized state, but it is created.
        /// </summary>
        /// <param name="registeredObject"></param>
        void AfterRegisterObject(object registeredObject);

        /// <summary>
        ///     Called after an instance of the object was unregistered with the object manager.
        /// </summary>
        /// <param name="removedObject"></param>
        void AfterUnregisterObject(object removedObject);
    }

    /// <summary>
    ///     Interface for a manager of arbitrary game objects.
    /// </summary>
    public interface IObjectManager
    {
        /// <summary>
        ///     Returns whether this object manager can manage objects of the given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool Manages<T>();

        /// <summary>
        ///     Register an observer for a given type.
        /// </summary>
        /// <param name="observer"></param>
        /// <typeparam name="T"></typeparam>
        void Register<T>(IObjectLifetimeObserver observer);

        /// <summary>
        ///     Removes a registered observer.
        /// </summary>
        /// <param name="observer"></param>
        void Unregister(IObjectLifetimeObserver observer);
    }
}