using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests
{
    public interface IDummyClass { }
    public class DummyClass : IDummyClass
    {
        public DummyClass()
        {
            ;
        }
    }

    public class asdfasdf
    {
        [Fact]
        public void Test()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterType(typeof(DummyClass)).As<IDummyClass>().SingleInstance();

            IContainer container = builder.Build();

            IDummyClass t = container.Resolve<IDummyClass>();

            
        }
        
    }
}
