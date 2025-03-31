using Microsoft.Extensions.DependencyInjection;

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
  public async Task Execute(IServiceProvider? serviceProvider, CancellationToken token)
  {
    var job = CreateJob(serviceProvider);
    await job.RunAsync();
  }

  private static T CreateJob(IServiceProvider? serviceProvider)
  {
    if (serviceProvider == null) return Activator.CreateInstance<T>();
    using var scope = serviceProvider.CreateScope();
    var job = ActivatorUtilities.CreateInstance<T>(scope.ServiceProvider);
    return job;
  }
}