using Microsoft.Extensions.DependencyInjection;

namespace Hsp.JobScheduler;

public interface IJobTask : IAsyncDisposable
{
  Task RunAsync(IServiceScope? scope, CancellationToken token);
}