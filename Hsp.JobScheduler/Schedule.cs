namespace Hsp.JobScheduler;

public class Schedule
{
  private DateTimeOffset? _lastRunTime;

  /// <summary>
  /// Specifies the earliest time the job can start.
  /// </summary>
  public DateTimeOffset? EarliestStartTime { get; }

  /// <summary>
  /// Specifies the cron expression for the job.
  /// If this is not specified, the job will be a one-time job.
  /// If this is specified, the job will run at the specified frequency. 
  /// </summary>
  public string? CronExpression { get; }

  /// <summary>
  /// An optional jitter value that will be added when the next run time is calculated.
  /// </summary>
  public TimeSpan? Jitter { get; }

  /// <summary>
  /// The next calculated run time for the job.
  /// You can manually override this, but any subsequent updates to the last run time will recalculate this value.
  /// </summary>
  public DateTimeOffset NextRunTime { get; set; }

  /// <summary>
  /// The next calculated run time for the job.
  /// </summary>
  public DateTimeOffset? LastRunTime
  {
    get => _lastRunTime;
    set
    {
      _lastRunTime = value;
      if (_lastRunTime != null)
        UpdateNextRunTime(_lastRunTime.Value);
    }
  }


  public Schedule(string? cronExpression, DateTimeOffset? earliestStartTime, TimeSpan? jitter = null)
  {
    EarliestStartTime = earliestStartTime;
    CronExpression = cronExpression;
    Jitter = jitter;
    UpdateNextRunTime(DateTimeOffset.MinValue);
  }

  public Schedule(string cronExpression, TimeSpan? jitter = null)
    : this(cronExpression, null, jitter)
  {
  }

  public Schedule(DateTimeOffset earliestStartTime, TimeSpan? jitter = null)
    : this(null, earliestStartTime, jitter)
  {
  }


  /// <summary>
  /// Updates the next run time based on the last run time.
  /// </summary>
  /// <param name="refTime">The reference time to use.</param>
  public void UpdateNextRunTime(DateTimeOffset refTime)
  {
    NextRunTime = CalculateNextRunTime(refTime) ?? refTime;
    if (Jitter == null) return;

    var random = new Random();
    var jv = Jitter.Value.TotalMilliseconds;
    jv = random.NextDouble() * (jv * 2) - jv;
    NextRunTime = NextRunTime.Add(TimeSpan.FromMilliseconds(jv));
  }


  private DateTimeOffset? CalculateNextRunTime(DateTimeOffset refTime)
  {
    var earliestStartTime = EarliestStartTime ?? DateTimeOffset.MinValue;

    if (CronExpression == null) return earliestStartTime;
    if (!Cronos.CronExpression.TryParse(CronExpression, out var exp)) return earliestStartTime;

    var nextRuntimeUtc = exp.GetNextOccurrence(refTime.UtcDateTime);
    if (nextRuntimeUtc == null) return earliestStartTime;
    var nextRuntime = new DateTimeOffset(nextRuntimeUtc.Value, TimeSpan.Zero);
    return nextRuntime < earliestStartTime ? earliestStartTime : nextRuntime;
  }
}