using Polly;

namespace Hsp.JobScheduler;

public interface IJobTask : IAsyncDisposable
{
  Task RunAsync(Context execution, CancellationToken token);
}