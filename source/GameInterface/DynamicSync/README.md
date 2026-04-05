# Dynamic Sync

1. [Collects]() all classes that inherit [IDynamicSync]()
2. Each field/propery/collection added from the IDynamicSync class is stored in the [DynamicSyncRegistry]()


There are 4 different types of data
1. Fields
2. Properties
3. Lists
4. 
3. After all IDynamicSync classes are instantiated, DynamicSyncPatchBuilder creates the following

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
public class MyDynamicSyncClass : IDynamicSync
{
	public MyDynamicSyncClass(DynamicSyncRegistry dynamicSyncRegistry)
    {
        dynamicSyncRegistry.AddField(typeof(MyClass), nameof(MyClass.MyInt));
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

To update automatically from an external class, use AddTargetMethod in the IDynamicSync class

```CS
	var method = AccessTools.Method(typeof(SomeOtherClass), nameof(SomeOtherClass.UpdateValue));

	dynamicSyncRegistry.AddTargetMethod(typeof(SomeOtherClass), method);
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
public class MyDynamicSyncClass : IDynamicSync
{
	public MyDynamicSyncClass(DynamicSyncRegistry dynamicSyncRegistry)
    {
        dynamicSyncRegistry.AddProperty(typeof(MyClass), nameof(MyClass.MyIntProperty));
    }
}
```

## Collections

### Array
### List
### MBList
### Queue