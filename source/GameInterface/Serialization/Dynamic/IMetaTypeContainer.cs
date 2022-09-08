namespace GameInterface.Serialization.Dynamic
{
    public interface IMetaTypeContainer
    {
        IMetaTypeContainer AddDerivedType<T>();
        IMetaTypeContainer UseConstuctor();
    }
}