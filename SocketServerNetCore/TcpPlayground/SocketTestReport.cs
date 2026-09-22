namespace SocketServerNetCore.TcpPlayground;

public sealed class SocketTestReport
{
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int ServerPort { get; set; }
    public string ReportPath { get; set; } = string.Empty;
    public List<ScenarioResult> Scenarios { get; set; } = new();
    public bool AllPassed => Scenarios.All(scenario => scenario.Passed);
}

public sealed class ScenarioResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double DurationMs { get; set; }
    public List<string> Failures { get; set; } = new();
    public List<string> Diagnostics { get; set; } = new();
    public Dictionary<string, List<TcpTranscriptEvent>> ClientTranscripts { get; set; } = new();
}
