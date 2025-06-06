using Polly;

namespace Hsp.JobScheduler;

/// <summary>
/// A definition of a job that can be executed by the scheduler.
/// </summary>
public interface IJobDefinition
{
  /// <summary>
  /// The ID of the definition.
  /// </summary>
  string Id { get; }

  /// <summary>
  /// The name of the definition.
  /// </summary>
  string Name { get; }

  /// <summary>
  /// The schedule of the job.
  /// </summary>
  Schedule? Schedule { get; }

  /// <summary>
  /// Specifies a retry policy to use if an execution fails.
  /// If this is not specified, the action is not retried on failures.
  /// </summary>
  IAsyncPolicy? RetryPolicy { get; }

  /// <summary>
  /// Specifies if multiple instances of the job can run at the same time.
  /// </summary>
  bool ExecutionsCanOverlap { get; set; }


  /// <summary>
  /// Runs the job.
  /// </summary>
  /// <param name="execution">The execution instance.</param>
  /// <param name="serviceProvider"></param>
  /// <param name="token"></param>
  /// <returns></returns>
  Task Execute(JobExecution execution, IServiceProvider? serviceProvider, CancellationToken token);
}