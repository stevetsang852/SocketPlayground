using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommonClassLibrary;
using Payload.Core.Command;
using PayloadNetFrameworkTest.UnitTestClass.Interface;

namespace PayloadNetFrameworkTest.UnitTestClass
{
    [TestClass]
    public class StartProcessTest : BaseUnitTest
    {
        [TestMethod]
        public void TestStartProcessCommand()
        {
            //SetUp
            base.WorkingPathSetUp();

            //Arrange
            CopyItselfCommand copyItselfCommand = new CopyItselfFactory().CreateCommand();
            StartProcessCommand startProcessCommand = new StartProcessFactory().CreateCommand();
            RegistryKeyCommand registryKeyCommand = new RegistryKeyFactory().CreateCommand();
            registryKeyCommand.UacLevel = UAC_TYPE.HIGH;

            //Act
            //registryKeyCommand.Execute();
            //copyItselfCommand.Execute();
            startProcessCommand.Execute();


            //Assert

            //TearDown
            //copyItselfCommand.Undo();
        }
    }
}
