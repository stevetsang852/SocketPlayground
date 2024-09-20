using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonClassLibrary;

namespace PayloadNetFrameworkTest.UnitTestClass.Interface
{
    public class BaseUnitTest
    {
        public virtual void WorkingPathSetUp()
        {
            Config.Instance.WorkSpaceDir = new DirectoryInfo($"{Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName}\\PayloadNetFramework\\bin\\Debug").FullName;
        }
    }
}
