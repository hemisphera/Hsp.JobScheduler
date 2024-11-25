namespace Hsp.JobScheduler;

public interface IJobSchedulerNotifier
{
  Task OnDefinitionAdded(SimpleJobScheduler scheduler, IJobDefinition jobs);
  Task OnDefinitionRemoved(SimpleJobScheduler scheduler, IJobDefinition tr);
  Task OnSchedulerStarted(SimpleJobScheduler scheduler);
  Task OnSchedulerStopped(SimpleJobScheduler scheduler);
  Task OnJobStarted(JobExecution execution);
  Task OnJobFinished(JobExecution execution);
}