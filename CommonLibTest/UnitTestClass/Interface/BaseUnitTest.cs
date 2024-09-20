using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonClassLibrary;

namespace CommonLibTest.Interface
{
    public class BaseUnitTest
    {
        public virtual void WorkingPathSetUp()
        {
            Config.Instance.WorkSpaceDir = new DirectoryInfo($"{Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName}\\PayloadNetNode\\bin\\Debug\\net8.0").FullName;
            Console.WriteLine($"Working Path: {Config.Instance.WorkSpaceDir}");
        }
    }
}
