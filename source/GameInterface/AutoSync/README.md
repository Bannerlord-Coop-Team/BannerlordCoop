# Dynamic Sync

1. [Collects]() all classes that inherit [IAutoSync]()
2. Each field/propery/collection added from the IAutoSync class is stored in the [AutoSyncRegistry]()


There are 4 different types of data
1. Fields
2. Properties
3. Lists
4. 
3. After all IAutoSync classes are instantiated, AutoSyncPatchBuilder creates the following

	1. Handlers
	2. Messages
	3. Patches


## Syncing Fields

```CS
public class MyClass
{
	public int MyInt = 5;

	public void UpdateMyValue(int newValue)
	{
		MyInt = newValue
	}
}
```

To automatically sync a field
```CS
public class MyAutoSyncClass : IAutoSync
{
	public MyAutoSyncClass(AutoSyncRegistry autoSyncRegistry)
    {
        autoSyncRegistry.AddField(typeof(MyClass), nameof(MyClass.MyInt));
    }
}
```


**Warning:** Patches generated are only intercepting setting the value in the containing class

For example, the following will automatically be updated across the network
```CS
public class MyClass
{
	public int MyInt = 5;

	public void UpdateMyValue(int newValue)
	{
		MyInt = newValue
	}
}
```

However, if used by an external class. The value will not be updated automatically
```CS
public class SomeOtherClass
{
	public MyClass Instance;

	public void UpdateValue(int newValue)
	{
		Instance.MyInt = newValue
	}
}
```

To update automatically from an external class, use AddTargetMethod in the IAutoSync class

```CS
	var method = AccessTools.Method(typeof(SomeOtherClass), nameof(SomeOtherClass.UpdateValue));

	autoSyncRegistry.AddTargetMethod(typeof(SomeOtherClass), method);
```

## Properties
```CS
public class MyClass
{
	public int MyIntProperty { get; set; }
}
```

To automatically sync a property
```CS
public class MyAutoSyncClass : IAutoSync
{
	public MyAutoSyncClass(AutoSyncRegistry autoSyncRegistry)
    {
        autoSyncRegistry.AddProperty(typeof(MyClass), nameof(MyClass.MyIntProperty));
    }
}
```

## Collections

### Array
### List
### MBList
### Queue