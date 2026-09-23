using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommonClassLibrary;
using Payload.Core.Command;
using CommonLibTest.Interface;

namespace CommonLibTest
{
    [TestClass]
    public class StartProcessTest : BaseUnitTest
    {
        [TestMethod]
        public void TestCallAdminCommand()
        {
            TestEnvironmentRequirements.RequireWindows();
            CallAdminCommand cmd = new CallAdminCommand();
            cmd.Execute();
        }

        [TestMethod]
        public void TestStartProcessCommand()
        {
            TestEnvironmentRequirements.RequireWindows();
            //SetUp
            base.WorkingPathSetUp();

            //Arrange
            RegistryKeyCommand registryKeyCommand = new RegistryKeyFactory().CreateCommand();
            registryKeyCommand.UacLevel = UAC_TYPE.HIGH;
            CopyItselfCommand copyItselfCommand = new CopyItselfFactory().CreateCommand();
            StartProcessCommand startProcessCommand = new StartProcessFactory().CreateCommand();

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
