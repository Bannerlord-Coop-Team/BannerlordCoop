namespace GameInterface.AutoSync.Builders;
internal class AutoSyncTestClass
{
    public string Name = "hi";
    public int MyInt = 1;
    public AutoSyncRefClass? RefClass = null;
    public int MyProp { get; set; }

    public void SetMyInt(int val) { MyInt = val; }
}

internal class AutoSyncRefClass { }
