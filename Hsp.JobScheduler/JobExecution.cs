using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hsp.JobScheduler;

/// <summary>
/// Represents a job instance that was started by the scheduler.
/// </summary>
public class JobExecution
{
  /// <summary>
  /// The logger assigned to this execution.
  /// </summary>
  protected ILogger<JobExecution> Logger { get; }

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
  public DateTimeOffset StartTime { get; }

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
  public DateTimeOffset? FinishTime { get; private set; }

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
    StartTime = scheduler.Clock.GetUtcNow();
    Definition = definition;
    _serviceProvider = serviceProvider;
    CancellationTokenSource = tokenSource;
    Logger = serviceProvider?.GetService<ILogger<JobExecution>>() ?? new NullLogger<JobExecution>();
    Task = Execute(scheduler.Clock);
  }


  private async Task Execute(TimeProvider clock)
  {
    var schedule = Definition.Schedule;
    if (schedule != null)
      schedule.LastRunTime = clock.GetUtcNow();

    var failure = false;
    var propertydict = new Dictionary<string, object>
    {
      { "executionId", Id },
      { "definitionId", Definition.Id },
      { "definitionName", Definition.Name }
    };

    using var loggerScope = Logger.BeginScope(propertydict);

    try
    {
      Scheduler.RaiseOnJobStarted(this);
      Logger.LogInformation("Starting job execution for definition {definitionId}.", Definition.Id);
      using var scope = _serviceProvider?.CreateScope();
      await Definition.Execute(this, scope?.ServiceProvider, CancellationTokenSource.Token);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Job failed for definition {definitionId} failed.", Definition.Id);
      failure = true;
      Error = ex;
    }
    finally
    {
      FinishTime = clock.GetUtcNow();
      var nextExecution = Definition.Schedule?.NextRunTime.UtcDateTime;
      if (nextExecution != null)
        propertydict.Add("nextExecution", nextExecution.Value);
      propertydict.Add("success", !failure);
      Logger.LogInformation("Finished job execution for definition {definitionId} in {duration}ms.",
        Definition.Id,
        Duration?.TotalMilliseconds
      );
      Scheduler.RaiseOnJobCompleted(this);
    }
  }
}