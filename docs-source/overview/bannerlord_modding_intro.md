## How Mods are Loaded

Mods are packaged in the *Modules* folder with along with a respective [SubModule.xml](https://github.com/Bannerlord-Coop-Team/BannerlordCoop/blob/development/template/SubModule.xml) (where ${name} is the name of your module folder).<br/>
Bannerlord loads these modules at startup.<br/>
<br/>
[External Documentation](https://github.com/Bannerlord-Modding/Documentation/blob/master/_tutorials/basic-csharp-mod.md)

## Reading Bannerlord Code

We recommend using [dnspy](/overview/Tools/dnspy.html) to read Bannerlord dlls.<br/>

## Accessing Private Variables/Classes

**Reading and Writing to a Private Variable**

```CS
using System;
using System.Reflection;

class Foo {
    private string _bar = "private bar";

    public void printBar()
    {
        Console.WriteLine(_bar);
    }
}

class MainClass {
  public static void Main (string[] args) {
    // Create instance
    Foo foo = new Foo();

    foo.printBar(); // -> private bar
```

FieldInfo is used to access variable information of a given class
We have to specify NonPublic as BindingFlags for "GetField" default to Public and Instance

```CS
    FieldInfo fieldInfo = typeof(Foo).GetField("_bar", BindingFlags.NonPublic | BindingFlags.Instance);
``` 

Reading value needs a instance (or null if static)

```CS
    string bar = (string)fieldInfo.GetValue(foo);
    Console.WriteLine(bar); // -> private bar
```

Setting values needs a instance to set (or null if static) and a value.

```CS
    fieldInfo.SetValue(foo, "I set a new value!");
    foo.printBar(); // -> I set a new value!
  }
}
```
<br/>
<details>
<summary>**Full Class**</summary> 
<p>
```CS
using System;
using System.Reflection;

class Foo {
	private string _bar = "private bar";

	public void printBar()
	{
		Console.WriteLine(_bar);
	}
}

class MainClass {
  public static void Main (string[] args) {
	// Create instance
	Foo foo = new Foo();

	foo.printBar();

	// FieldInfo is used to access variable information of a given class
	// We have to specify NonPublic as BindingFlags for "GetField" default to Public and Instance
	FieldInfo fieldInfo = typeof(Foo).GetField("_bar", BindingFlags.NonPublic | BindingFlags.Instance);
	
	// Reading value needs a instance (or null if static)
	string bar = (string)fieldInfo.GetValue(foo);
	Console.WriteLine(bar);

	// Setting values needs a instance to set (or null if static) and a value.
	fieldInfo.SetValue(foo, "I set a new value!");
	foo.printBar();
	
  }
}
```
</p>
</details>
<br/>
**Instantiating a Internal/Private Class**

Inaccessible code
```CS
namespace ConsoleApp.Private
{
    class PrivateClass
    {
        public int someVar = 5;
    }
}
```

Creating an instance with Activator
```CS
using System;
using System.Reflection;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Activator
            // Need to get assembly when getting type, need full namespace type name as well.
            // Assembly may be different, you can pull assembly off public types
            Type privateType = Assembly.GetCallingAssembly().GetType("ConsoleApp.Private.PrivateClass");
            // Create class given a type
            object privateClass = Activator.CreateInstance(privateType);

            // Get someVar field value and print it
            Console.WriteLine(privateType.GetField("someVar").GetValue(privateClass));

            Console.ReadKey();
        }
    }
}
```

Creating an instance with constructor
```CS
using System;
using System.Reflection;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Get the constructor and invoke it
            // Need to get assembly when getting type, need full namespace type name as well.
            // Assembly may be different, you can pull assembly off public types
            Type privateType = Assembly.GetCallingAssembly().GetType("ConsoleApp.Private.PrivateClass");
            // Get constructor with no arguments
            ConstructorInfo privateTypeCtor = privateType.GetConstructor(new Type[0]);
            // Invoke constructor with no arguments
            object privateClass = privateTypeCtor.Invoke(new object[0]);

            // Get someVar field value and print it
            Console.WriteLine(privateType.GetField("someVar").GetValue(privateClass));

            Console.ReadKey();
        }
    }
}
```

## Patching Existing Code

There is existing Bannerlord code that you may want to change. For this we use [Harmony](./Tools/harmony.html).