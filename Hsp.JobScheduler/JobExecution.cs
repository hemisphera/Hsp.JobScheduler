using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hsp.JobScheduler;

/// <summary>
/// Represents a job instance that was started by the scheduler.
/// </summary>
public class JobExecution
{
  private readonly ILogger<JobExecution>? _logger;

  private readonly IServiceProvider? _serviceProvider;

  /// <summary>
  /// The definition that started the job.
  /// </summary>
  public IJobDefinition Definition { get; }

  public Task Task { get; private set; }

  public SimpleJobScheduler Scheduler { get; }

  /// <summary>
  /// The ID of the job instance.
  /// </summary>
  public Guid Id { get; } = Guid.NewGuid();

  /// <summary>
  /// The cancellation token source for this job.
  /// </summary>
  public CancellationTokenSource CancellationTokenSource { get; init; }

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


  internal static JobExecution Start(SimpleJobScheduler scheduler, IJobDefinition definition, IServiceProvider? serviceProvider, CancellationTokenSource token)
  {
    var instance = new JobExecution(scheduler, definition, serviceProvider, token);
    return instance;
  }


  private JobExecution(SimpleJobScheduler scheduler, IJobDefinition definition, IServiceProvider? serviceProvider, CancellationTokenSource tokenSource)
  {
    Scheduler = scheduler;
    Definition = definition;
    _serviceProvider = serviceProvider;
    CancellationTokenSource = tokenSource;
    _logger = serviceProvider?.GetService<ILogger<JobExecution>>();
    Task = Execute();
  }


  private async Task Execute()
  {
    var schedule = Definition.Schedule;
    if (schedule != null)
      schedule.LastRunTime = DateTime.Now;

    try
    {
      Scheduler.RaiseOnJobStarted(this);
      _logger?.LogInformation("Starting job execution {id} for definition {definitionId} {definitionName}.", Id, Definition.Id, Definition.Name);
      using var scope = _serviceProvider?.CreateScope();
      await Definition.Execute(scope?.ServiceProvider, CancellationTokenSource.Token);
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Job failed {id} for definition {definitionId} {definitionName} failed.", Id, Definition.Id, Definition.Name);
      Error = ex;
    }
    finally
    {
      FinishTime = DateTime.Now;
      _logger?.LogInformation("Finished job execution {id} for definition {definitionId} {definitionName} in {duration}ms.",
        Id,
        Definition.Id,
        Definition.Name,
        Duration?.TotalMilliseconds
      );
      Scheduler.RaiseOnJobCompleted(this);
    }
  }
}