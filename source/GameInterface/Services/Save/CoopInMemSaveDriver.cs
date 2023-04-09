using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Save
{
    internal class CoopInMemSaveDriver : InMemDriver
    {
        private static readonly FieldInfo _data = typeof(InMemDriver).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

        public CoopInMemSaveDriver()
        {
        }

        public CoopInMemSaveDriver(byte[] saveData)
        {
            _data.SetValue(this, saveData);
        }

        public byte[] Data 
        { 
            get 
            { 
                return (byte[])_data.GetValue(this);
            }
        }
    }
}
