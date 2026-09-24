using System.Text.Json;
using Payload;
using Payload.Core.Command;

namespace CommonLibTest;

[TestClass]
public sealed class CSharpExecutionResultTests
{
    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void GoodSnippet_ExecutedTrue_NoExecutionError()
    {
        var bridge = new LegacyCommandBridge();
        var result = JsonSerializer.SerializeToElement(bridge.HandleCSharp(
            JsonSerializer.SerializeToElement(new
            {
                data = "Console.WriteLine(1 + 1); Console.WriteLine(\"hello-from-csharp\");"
            })));

        Assert.AreEqual("executed", result.GetProperty("status").GetString());
        Assert.AreEqual(true, result.GetProperty("executed").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("executionError").ValueKind);
        var stdOut = result.GetProperty("stdOut").GetString() ?? string.Empty;
        StringAssert.Contains(stdOut, "2");
        StringAssert.Contains(stdOut, "hello-from-csharp");
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void CompileError_ExecutedFalse_ExecutionErrorContainsDiagnostic()
    {
        var bridge = new LegacyCommandBridge();
        var result = JsonSerializer.SerializeToElement(bridge.HandleCSharp(
            JsonSerializer.SerializeToElement(new { data = "int x = ;" })));

        Assert.AreEqual("executed", result.GetProperty("status").GetString());
        Assert.AreEqual(false, result.GetProperty("executed").GetBoolean());
        StringAssert.Contains(result.GetProperty("executionError").GetString() ?? string.Empty, "CS1525");
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void RuntimeException_ExecutedFalse_ExecutionErrorContainsBoom()
    {
        var bridge = new LegacyCommandBridge();
        var result = JsonSerializer.SerializeToElement(bridge.HandleCSharp(
            JsonSerializer.SerializeToElement(new { data = "throw new Exception(\"boom\");" })));

        Assert.AreEqual("executed", result.GetProperty("status").GetString());
        Assert.AreEqual(false, result.GetProperty("executed").GetBoolean());
        StringAssert.Contains(result.GetProperty("executionError").GetString() ?? string.Empty, "boom");
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void ResultStatusRemainsExecuted_NotRejected_ForCompileAndRuntimeFailures()
    {
        // CanonicalTcpClient only sets success=false when result.status == "rejected".
        var bridge = new LegacyCommandBridge();
        foreach (var data in new[]
                 {
                     "Console.WriteLine(1);",
                     "int x = ;",
                     "throw new Exception(\"x\");"
                 })
        {
            var result = JsonSerializer.SerializeToElement(
                bridge.HandleCSharp(JsonSerializer.SerializeToElement(new { data })));
            Assert.AreEqual("executed", result.GetProperty("status").GetString(), data);
        }
    }

    [TestMethod]
    [TestCategory("PayloadCanonical")]
    public void ExecuteWithResult_DirectApi_MatchesBridgeShape()
    {
        var ok = new CSharpExecuteCommand
        {
            TargetCSharpCode = "Console.WriteLine(\"ok\");"
        }.ExecuteWithResult();
        Assert.IsTrue(ok.Executed);
        Assert.IsNull(ok.ExecutionError);

        var bad = new CSharpExecuteCommand
        {
            TargetCSharpCode = "int x = ;"
        }.ExecuteWithResult();
        Assert.IsFalse(bad.Executed);
        StringAssert.Contains(bad.ExecutionError ?? string.Empty, "CS1525");
    }
}
