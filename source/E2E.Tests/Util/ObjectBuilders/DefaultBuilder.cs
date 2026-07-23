namespace E2E.Tests.Util.ObjectBuilders
{
    internal class DefaultBuilder<TObject> : IObjectBuilder where TObject: new()
    {
        public object Build()
        {
            return new TObject();
        }
    }
}
