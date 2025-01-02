using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Hsp.JobScheduler;

/// <summary>
/// A simple job scheduler that can run jobs at a specified frequency.
/// </summary>
public class SimpleJobScheduler
{
  private readonly ILogger? _logger;
  private readonly IServiceProvider? _serviceProvider;
  private readonly List<IJobDefinition> _definitions = [];
  private readonly SynchronizedCollection<JobExecution> _executions = [];
  private readonly SynchronizedCollection<Guid> _forceStartJobs = [];
  private readonly SemaphoreSlim _jobLock = new(1, 1);

  private CancellationTokenSource _cancellationTokenSource = new();

  /// <summary>
  /// Indicates if the scheduler is currently running.
  /// </summary>
  public bool IsRunning { get; private set; }


  public event EventHandler<JobExecution>? OnJobStarted;

  public event EventHandler<JobExecution>? OnJobCompleted;


  /// <summary>
  /// </summary>
  /// <param name="serviceProvider">Specifies an optional service provider to use for service construction.</param>
  public SimpleJobScheduler(IServiceProvider? serviceProvider = null)
  {
    _logger = serviceProvider?.GetService<ILogger<SimpleJobScheduler>>();
    _serviceProvider = serviceProvider;
  }


  /// <summary>
  /// Returns a list of all jobs.
  /// </summary>
  /// <returns>And array of jobs.</returns>
  public async Task<IJobDefinition[]> Get()
  {
    return await Get(_ => true);
  }

  /// <summary>
  /// Returns a list of jobs that match the filter.
  /// </summary>
  /// <param name="filter">The filter to apply to the jobs.</param>
  /// <returns>And array of jobs.</returns>
  public async Task<IJobDefinition[]> Get(Predicate<IJobDefinition> filter)
  {
    try
    {
      await _jobLock.WaitAsync();
      return _definitions.Where(f => filter(f)).ToArray();
    }
    finally
    {
      _jobLock.Release();
    }
  }

  /// <summary>
  /// Gets a job definition by ID.
  /// </summary>
  /// <param name="definitionId"></param>
  /// <returns></returns>
  public async Task<IJobDefinition?> Get(Guid definitionId)
  {
    var candidates = await Get(job => job.Id == definitionId);
    return candidates.FirstOrDefault();
  }

  /// <summary>
  /// Gets a list of job instances for a given definition that match the filter.
  /// </summary>
  /// <param name="definitionId">The ID of the job definition to get instances for.</param>
  /// <param name="filter">An optional filter to apply to the instances.</param>
  /// <returns>The list of job instances.</returns>
  public JobExecution[] GetExecutions(Guid definitionId, Predicate<JobExecution>? filter = null)
  {
    return _executions
      .Where(i => i.Definition.Id == definitionId)
      .Where(i => filter == null || filter(i))
      .OrderByDescending(i => i.StartTime)
      .ToArray();
  }

  /// <summary>
  /// Adds a scheduled job definition to the scheduler.
  /// </summary>
  /// <param name="jobDefinition">The job definition to add.</param>
  /// <returns></returns>
  public Task Add(IJobDefinition jobDefinition)
  {
    return Add([jobDefinition]);
  }

  /// <summary>
  /// Adds a list of scheduled job definitions to the scheduler.
  /// </summary>
  /// <param name="jobs">The job definitions to add.</param>
  public async Task Add(IEnumerable<IJobDefinition> jobs)
  {
    try
    {
      await _jobLock.WaitAsync();
      foreach (var job in jobs)
      {
        _definitions.Add(job);
      }
    }
    finally
    {
      _jobLock.Release();
    }
  }

  /// <summary>
  /// Removes a scheduled job definition from the scheduler.
  /// </summary>
  /// <param name="definition">The job definition to remove.</param>
  public Task Remove(IJobDefinition definition)
  {
    return Remove([definition.Id]);
  }

  /// <summary>
  /// Removes a scheduled job definition from the scheduler.
  /// </summary>
  /// <param name="definitionId">The ID of the job definition to remove.</param>
  public Task Remove(Guid definitionId)
  {
    return Remove([definitionId]);
  }

  /// <summary>
  /// Removes a list of scheduled job definitions from the scheduler.
  /// </summary>
  /// <param name="definitions">The job definitions to remove.</param>
  public Task Remove(IEnumerable<IJobDefinition> definitions)
  {
    return Remove(definitions.Select(definition => definition.Id));
  }

  /// <summary>
  /// Removes a list of scheduled job definitions from the scheduler.
  /// </summary>
  /// <param name="definitionIds">The job definitions to remove.</param>
  public async Task Remove(IEnumerable<Guid> definitionIds)
  {
    var idsArray = definitionIds.ToArray();
    var jobsToRemove = await Get(def => idsArray.Contains(def.Id));
    try
    {
      await _jobLock.WaitAsync();
      foreach (var tr in jobsToRemove)
      {
        _definitions.Remove(tr);
      }
    }
    finally
    {
      _jobLock.Release();
    }
  }


  /// <summary>
  /// Stops the scheduler.
  /// </summary>
  public async Task Stop()
  {
    if (!IsRunning) return;
    await _cancellationTokenSource.CancelAsync();
    await Task.WhenAll(_executions.Where(i => i.Running).ToArray().Select(async i => await i.Task));
    IsRunning = false;
    _logger?.LogInformation("The job scheduler has stopped.");
  }

  /// <summary>
  /// Starts the scheduler.
  /// </summary>
  public async Task Start()
  {
    if (IsRunning) return;
    IsRunning = true;
    _cancellationTokenSource = new CancellationTokenSource();

    _ = Task.Run(async () =>
    {
      var token = _cancellationTokenSource.Token;
      while (!token.IsCancellationRequested)
      {
        await Task.Delay(1000, token);

        var definitions = await Get(CanRunJob);
        foreach (var definition in definitions)
        {
          var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token);
          var execution = JobExecution.Start(this, definition, _serviceProvider, linkedToken);
          _logger?.LogInformation("Execution {id} has been started for definition {definitionId}.", execution.Id, definition.Id);
          _executions.Add(execution);
        }

        await Remove(await Get(IsExpired));
      }
    });

    _logger?.LogInformation("The job scheduler has started.");
    await Task.CompletedTask;
  }

  /// <summary>
  /// Forcibly starts a given job definition.
  /// </summary>
  /// <param name="definitionId">The ID of the job definition to start.</param>
  public void ForceStart(Guid definitionId)
  {
    if (_forceStartJobs.Contains(definitionId)) return;
    if (_definitions.FirstOrDefault(a => a.Id == definitionId) == null) return;
    _forceStartJobs.Add(definitionId);
    _logger?.LogInformation("Forced job execution for {id} has been received.", definitionId);
  }

  private bool CanRunJob(IJobDefinition definition)
  {
    if (_forceStartJobs.Contains(definition.Id))
    {
      _forceStartJobs.Remove(definition.Id);
      return true;
    }

    if (!definition.ExecutionsCanOverlap)
    {
      var executions = GetExecutions(definition.Id);
      if (executions.Any(i => i.Running)) return false;
    }

    var nextRunTime = definition.Schedule?.NextRunTime ?? DateTime.Now;
    return DateTime.Now >= nextRunTime;
  }

  private bool IsExpired(IJobDefinition jobDefinition)
  {
    var instances = GetExecutions(jobDefinition.Id);
    if (instances.Any(i => i.Running)) return false;
    return jobDefinition.Schedule?.CronExpression == null;
  }

  internal void RaiseOnJobStarted(JobExecution jobExecution)
  {
    OnJobStarted?.Invoke(this, jobExecution);
  }

  internal void RaiseOnJobCompleted(JobExecution jobExecution)
  {
    OnJobCompleted?.Invoke(this, jobExecution);
  }
}