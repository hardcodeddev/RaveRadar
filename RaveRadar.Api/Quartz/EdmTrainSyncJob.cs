using Quartz;
using RaveRadar.Api.Services;

namespace RaveRadar.Api.Quartz;

[DisallowConcurrentExecution]
public class EdmTrainSyncJob : IJob
{
    private readonly EdmTrainService _edmTrainService;
    private readonly ILogger<EdmTrainSyncJob> _logger;

    public EdmTrainSyncJob(EdmTrainService edmTrainService, ILogger<EdmTrainSyncJob> logger)
    {
        _edmTrainService = edmTrainService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting EDM Train Sync Job...");
        
        // Sync general events (could be parameterized if needed)
        await _edmTrainService.SyncEvents();
        
        _logger.LogInformation("EDM Train Sync Job completed.");
    }
}
