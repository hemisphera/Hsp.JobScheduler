using Polly;

namespace Hsp.JobScheduler.Definitions;

/// <summary>
/// A definition of a scheduled job using an action as runner.
/// </summary>
public class ActionJobDefinition : IJobDefinition
{
  private readonly Func<Context, IServiceProvider?, CancellationToken, Task> _action;

  /// <inheritdoc />
  public string Name { get; }

  /// <inheritdoc />
  public string Id { get; }

  /// <inheritdoc />
  public Schedule? Schedule { get; }

  /// <inheritdoc />
  public IAsyncPolicy? RetryPolicy { get; set; }

  /// <inheritdoc />
  public bool ExecutionsCanOverlap { get; set; }

  /// <summary>
  /// </summary>
  /// <param name="id"></param>
  /// <param name="name"></param>
  /// <param name="schedule"></param>
  /// <param name="action">The action to run.</param>
  public ActionJobDefinition(string id, string name, Schedule? schedule, Func<Context, IServiceProvider?, CancellationToken, Task> action)
  {
    Name = name;
    Id = id;
    Schedule = schedule;
    _action = action;
  }


  /// <inheritdoc />
  public async Task Execute(Context context, IServiceProvider? serviceProvider, CancellationToken token)
  {
    var policy = RetryPolicy ?? Policy.NoOpAsync();
    await policy.ExecuteAsync(
      async (ctx, ct) => { await _action(ctx, serviceProvider, ct); },
      context, token);
  }
}