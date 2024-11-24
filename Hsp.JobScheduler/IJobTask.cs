namespace Hsp.JobScheduler;

public interface IJobTask
{
  Task RunAsync();
}