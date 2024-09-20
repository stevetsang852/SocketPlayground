using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommonClassLibrary;
using Payload.Core.Command;
using Payload.Core.Command.Demo;
using CommonLibTest.Interface;

namespace CommonLibTest
{
    [TestClass]
    public class CopyItseftUnitTest : BaseUnitTest
    {
        [TestMethod]
        public void TestCopyItseftCommand()
        {
            //SetUp
            base.WorkingPathSetUp();

            //Arrange
            CopyItselfCommand cmd = new CopyItselfFactory().CreateCommand();       

            //Act
            cmd.Execute();


            //Assert
            //Get all directories name.
            //string[] buildPath = Directory.GetDirectories(Config.Instance.WorkSpaceDir);
            List<string> buildPath_allFiles = Directory.GetFiles(Config.Instance.WorkSpaceDir).Select(x=> x.Replace(Config.Instance.WorkSpaceDir, "")).ToList<string>();
            //string[] targetWorkPath = Directory.GetDirectories(Config.Instance.TargetWorkSpaceDir);
            List<string> targetWorkPath_allFiles = Directory.GetFiles(Config.Instance.TargetWorkSpaceDir).Select(x => x.Replace(Config.Instance.TargetWorkSpaceDir, "")).ToList<string>();
            /*
            https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.except?view=net-8.0&redirectedfrom=MSDN#System_Linq_Enumerable_Except__1_System_Collections_Generic_IEnumerable___0__System_Collections_Generic_IEnumerable___0__System_Collections_Generic_IEqualityComparer___0__
            */
            var missedBuildFiles = buildPath_allFiles.Except(targetWorkPath_allFiles).ToList();
            Assert.AreEqual(0, missedBuildFiles.Count);

            //TearDown
            cmd.Undo();
        }
    }
}
