namespace GameInterface.Serialization
{
    public interface ISerializer
    {
        byte[] Serialize(object obj);

        T Deserialize<T>(byte[] data);
    }
}