namespace Hsp.JobScheduler;

public interface IJobTask : IAsyncDisposable
{
  Task RunAsync(JobExecution execution, CancellationToken token);
}