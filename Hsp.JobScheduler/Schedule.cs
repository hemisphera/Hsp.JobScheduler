namespace Hsp.JobScheduler;

public class Schedule
{
  private DateTime? _lastRunTime;

  /// <summary>
  /// Specifies the earliest time the job can start.
  /// </summary>
  public DateTime? EarliestStartTime { get; }

  /// <summary>
  /// Specifies the cron expression for the job.
  /// If this is not specified, the job will be a one-time job.
  /// IF this is specified, the job will run at the specified frequency. 
  /// </summary>
  public string? CronExpression { get; }

  /// <summary>
  /// An optional jitter value that will be added to the next run time.
  /// </summary>
  public TimeSpan? Jitter { get; }

  /// <summary>
  /// The next calculated run time for the job.
  /// </summary>
  public DateTime NextRunTime { get; private set; }

  /// <summary>
  /// The next calculated run time for the job.
  /// </summary>
  public DateTime? LastRunTime
  {
    get => _lastRunTime;
    set
    {
      if (_lastRunTime != null)
        UpdateNextRunTime(_lastRunTime.Value);
      _lastRunTime = value;
    }
  }

  public Schedule(string? cronExpression, DateTime? earliestStartTime, TimeSpan? jitter = null)
  {
    EarliestStartTime = earliestStartTime;
    CronExpression = cronExpression;
    Jitter = jitter;
    UpdateNextRunTime();
  }

  public Schedule(string cronExpression, TimeSpan? jitter = null)
    : this(cronExpression, null, jitter)
  {
  }

  public Schedule(DateTime earliestStartTime, TimeSpan? jitter = null)
    : this(null, earliestStartTime, jitter)
  {
  }


  /// <summary>
  /// Updates the next run time based on the last run time.
  /// </summary>
  /// <param name="lastRunTime">The last run time. If null, the current time is used.</param>
  public void UpdateNextRunTime(DateTime? lastRunTime = null)
  {
    var refTime = lastRunTime ?? DateTime.Now;
    NextRunTime = GetNextRunTime(refTime) ?? refTime;
    if (Jitter == null) return;

    var random = new Random();
    var jv = Jitter.Value.TotalMilliseconds;
    jv = random.NextDouble() * (jv * 2) - jv;
    NextRunTime = NextRunTime.Add(TimeSpan.FromMilliseconds(jv));
  }


  private DateTime? GetNextRunTime(DateTime refTime)
  {
    var fallBack = DateTime.Now.Subtract(TimeSpan.FromSeconds(1));
    var earliestStartTime = (EarliestStartTime ?? fallBack).ToUniversalTime();

    if (CronExpression == null) return earliestStartTime;
    if (!Cronos.CronExpression.TryParse(CronExpression, out var exp)) return earliestStartTime;

    var nextRuntime = exp.GetNextOccurrence(refTime.ToUniversalTime());
    if (nextRuntime == null) return earliestStartTime;
    return nextRuntime < earliestStartTime ? earliestStartTime : nextRuntime;
  }
}