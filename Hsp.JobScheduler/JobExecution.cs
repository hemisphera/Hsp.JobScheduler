using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hsp.JobScheduler;

/// <summary>
/// Represents a job instance that was started by the scheduler.
/// </summary>
public class JobExecution
{
  /// <summary>
  /// The definition that started the job.
  /// </summary>
  public IJobDefinition Definition { get; }

  /// <summary>
  /// The ID of the job instance.
  /// </summary>
  public Guid Id { get; } = Guid.NewGuid();

  /// <summary>
  /// The task that is running the job.
  /// </summary>
  public Task Task { get; }

  /// <summary>
  /// The cancellation token source for this job.
  /// </summary>
  public CancellationTokenSource CancellationTokenSource { get; }

  /// <summary>
  /// Specifies the time when the job started.
  /// </summary>
  public DateTime StartTime { get; } = DateTime.Now;

  /// <summary>
  /// Specifies if the job was successful.
  /// </summary>
  public bool? Success
  {
    get
    {
      if (FinishTime == null) return null;
      return Error == null;
    }
  }

  /// <summary>
  /// Specifies the error that occurred during the job.
  /// This will be null if the job was successful or is still running.
  /// </summary>
  public Exception? Error { get; private set; }

  /// <summary>
  /// Specifies the time when the job finished.
  /// This will be null if the job is still running.
  /// </summary>
  public DateTime? FinishTime { get; private set; }

  /// <summary>
  /// The duration of the job.
  /// This will be null if the job is still running.
  /// </summary>
  public TimeSpan? Duration => FinishTime - StartTime;

  /// <summary>
  /// Specifies if this instance is currently running.
  /// </summary>
  public bool Running => FinishTime == null;


  internal static JobExecution Start(IJobDefinition definition, IServiceProvider? serviceProvider, CancellationToken parentToken)
  {
    var logger = serviceProvider?.GetService<ILogger<JobExecution>>();
    var instance = new JobExecution(definition, async ct => { await definition.Execute(serviceProvider, ct); }, parentToken, logger);
    var schedule = definition.Schedule;
    if (schedule != null)
      schedule.LastRunTime = DateTime.Now;
    return instance;
  }


  private JobExecution(IJobDefinition definition, Func<CancellationToken, Task> func, CancellationToken parentToken, ILogger? logger)
  {
    Definition = definition;
    CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
    var newToken = CancellationTokenSource.Token;
    Task = Task.Run(async () =>
    {
      try
      {
        logger?.LogInformation("Starting job execution {id} for definition {definitionId} {definitionName}.", Id, definition.Id, definition.Name);
        await func(newToken);
      }
      catch (Exception ex)
      {
        logger?.LogError(ex, "Job failed {id} for definition {definitionId} {definitionName} failed.", Id, definition.Id, definition.Name);
        Error = ex;
      }
      finally
      {
        FinishTime = DateTime.Now;
        logger?.LogInformation("Finished job execution {id} for definition {definitionId} {definitionName} in {duration}ms.",
          Id,
          definition.Id,
          definition.Name,
          Duration?.TotalMilliseconds
        );
      }
    }, newToken);
  }
}