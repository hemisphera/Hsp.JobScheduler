using Polly;

namespace Hsp.JobScheduler;

/// <summary>
/// Extensions.
/// </summary>
public static class Extensions
{
  internal const string ExecutionContextKeyName = "execution";
  internal const string DefinitionContextKeyName = "definition";

  /// <summary>
  /// Get the JobExecution from the Context.
  /// </summary>
  /// <param name="ctx">The context.</param>
  /// <returns>An instance of <see cref="JobExecution"/> if it exists, otherwise null.</returns>
  public static JobExecution? GetExecution(this Context ctx)
  {
    return ctx.TryGetValue(ExecutionContextKeyName, out var execution) ? execution as JobExecution : null;
  }

  /// <summary>
  /// Get the JobDefinition from the Context.
  /// </summary>
  /// <param name="ctx">The context.</param>
  /// <returns>An instance of <see cref="IJobDefinition"/> if it exists, otherwise null.</returns>
  public static IJobDefinition? GetDefinition(this Context ctx)
  {
    return ctx.TryGetValue(DefinitionContextKeyName, out var execution) ? execution as IJobDefinition : null;
  }
}