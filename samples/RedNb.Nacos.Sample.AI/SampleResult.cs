namespace RedNb.Nacos.Sample.AI;

public enum SampleOutcome
{
    Ok,
    Skipped,
    Failed
}

public sealed record SampleResult(SampleOutcome Outcome, string? Message = null);
