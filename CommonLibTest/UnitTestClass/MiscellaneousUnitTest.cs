using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonClassLibrary;
using CommonLibTest.Interface;

namespace CommonLibTest.UnitTestClass
{
    [TestClass]
    public class MiscellaneousUnitTest : BaseUnitTest
    {
        [TestMethod]
        public void TestImageHelper()
        {
            TestEnvironmentRequirements.RequireExternalNetworkOptIn();
            base.WorkingPathSetUp();
            string url = "https://imgs.orientalsunday.hk/wp-content/uploads/2024/05/1cead865-c538-4097-9c7b-7648a8e076fc_85437503665829f4a0237.jpg";
            string savedPath = $"{Config.Instance.WallpaperEngineCommandImageDir}\\{DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_fff")}";
            new ImageHelper().SaveImage(url, savedPath);
            Console.WriteLine(savedPath);
            Assert.IsTrue(File.Exists(savedPath));
        }
    }
}
