using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonClassLibrary;
using CommonLibTest.Interface;
using Payload.Core.Command;

namespace CommonLibTest.UnitTestClass
{
    [TestClass]
    public class DllExecuteUnitTest : BaseUnitTest
    {
        [TestMethod]
        public void TestDllExecuteCommand()
        {
            //SetUp
            base.WorkingPathSetUp();

            //Arrange
            DllExecuteCommand cmd = new DllExecuteFactory().CreateCommand();
            cmd.TargetDLLPath = @"C:\Users\Andrew Tsang\Documents\GitHub\SocketPlayground\DLLLibrary\bin\Debug\net8.0\DLLLibrary.dll";
            cmd.TargetDllExecuteMode = DllExecuteMode.Command;
            //Act
            cmd.Execute();


            //Assert


            //TearDown
        }
    }
}
