using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Payload.Core.Command;
using Payload.Core.Command.Demo;

namespace CommonLibTest
{
    [TestClass]
    public class UnitTestDemo
    {
        [TestMethod]
        public void TestMethodDemoCommand()
        {
            DemoCommand cmd = new DemoFactory().CreateCommand();
            cmd.Execute();
            
            cmd.Undo();
        }
    }
}
