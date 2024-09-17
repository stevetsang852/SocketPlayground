using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Payload.Common;
using Payload.Core.Command;
using Payload.Core.Command.Demo;
using PayloadNetFrameworkTest.UnitTestClass.Interface;

namespace PayloadNetFrameworkTest
{
    [TestClass]
    public class RegistryKeyUnitTest : BaseUnitTest
    {
        [TestMethod]
        public void TestRegistryKeyCommand()
        {
            //SetUp
            base.WorkingPathSetUp();
            //Arrange
            RegistryKeyCommand cmd = new RegistryKeyFactory().CreateCommand();
            //Act
            cmd.Execute();

            //Assert

            //TearDown
        }

        [TestMethod]
        public void TestRegistryKeyBatCommand()
        {
            //SetUp
            base.WorkingPathSetUp();
            //Arrange
            RegistryKeyBatCommand cmd = new RegistryKeyBatFactory().CreateCommand();
            //Act
            //cmd.PrepareBat();
            cmd.Execute();

            //Assert

            //TearDown
        }
    }
}
