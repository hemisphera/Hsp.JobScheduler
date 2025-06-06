using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Hsp.JobScheduler.Definitions;

/// <summary>
/// A definition of a scheduled job using a generic task class as runner.
/// </summary>
/// <typeparam name="T"></typeparam>
public class TaskJobDefinition<T> : IJobDefinition where T : IJobTask
{
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
  public TaskJobDefinition(string id, string name, Schedule? schedule = null)
  {
    Name = name;
    Id = id;
    Schedule = schedule;
  }


  /// <inheritdoc />
  public async Task Execute(JobExecution execution, IServiceProvider? serviceProvider, CancellationToken token)
  {
    var policy = RetryPolicy ?? Policy.NoOpAsync();
    await policy.ExecuteAsync(async () =>
    {
      var task = serviceProvider == null
        ? ExecuteWithoutScope(execution, token)
        : ExecuteWithScope(execution, serviceProvider, token);
      await task;
    });
  }

  private static async Task ExecuteWithScope(JobExecution execution, IServiceProvider serviceProvider, CancellationToken token)
  {
    using var scope = serviceProvider.CreateScope();
    await using var job = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
    await job.RunAsync(execution, token);
  }

  private static async Task ExecuteWithoutScope(JobExecution execution, CancellationToken token)
  {
    await using var job = Activator.CreateInstance<T>();
    await job.RunAsync(execution, token);
  }
}