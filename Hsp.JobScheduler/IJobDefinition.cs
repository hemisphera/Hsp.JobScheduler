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
  /// Specifies if multiple instances of the job can run at the same time.
  /// </summary>
  bool ExecutionsCanOverlap { get; set; }


  /// <summary>
  /// Runs the job.
  /// </summary>
  /// <param name="serviceProvider"></param>
  /// <param name="token"></param>
  /// <returns></returns>
  Task Execute(IServiceProvider? serviceProvider, CancellationToken token);
}