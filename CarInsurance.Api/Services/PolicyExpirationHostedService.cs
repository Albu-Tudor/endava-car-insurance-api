using CarInsurance.Api.Data;
using CarInsurance.Api.Models;

using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace CarInsurance.Api.Services;

public interface IScopedProcessingService
{
    Task DoWork(CancellationToken stoppingToken);
}

public class ScopedProcessingService : IScopedProcessingService
{
    private readonly AppDbContext _db;
    private readonly ILogger _logger;

    public ScopedProcessingService(AppDbContext db, ILogger<ScopedProcessingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task DoWork(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                const string stateKey = "PolicyExpirationChecker.LastRunUtc";
                var state = await _db.ProcessingStates.FirstOrDefaultAsync(s => s.Key == stateKey);

                DateOnly lastRunUtc = state!.Value;
                var today = DateOnly.FromDateTime(DateTime.UtcNow.ToLocalTime().Date);

                var candidates = await _db.Policies
                    .Where(p => p.EndDate >= lastRunUtc
                        && p.EndDate < today)
                    .ToListAsync();

                foreach (var policy in candidates)
                {
                    _logger.LogInformation("Policy {PolicyId} from {Provider} expired on {EndDate}", policy.Id, policy.Provider ?? "Unknown", policy.EndDate);
                }

                state.Value = today;

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing policy expirations");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}

public class PolicyExpirationHostedService : BackgroundService
{
    private readonly ILogger<PolicyExpirationHostedService> _logger;

    public PolicyExpirationHostedService(IServiceProvider services,
        ILogger<PolicyExpirationHostedService> logger)
    {
        Services = services;
        _logger = logger;
    }

    public IServiceProvider Services { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Policy Expiration Hosted Service running.");

        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Policy Expiration Hosted Service is working.");

        using (var scope = Services.CreateScope())
        {
            var scopedProcessingService =
                scope.ServiceProvider
                    .GetRequiredService<IScopedProcessingService>();

            await scopedProcessingService.DoWork(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Policy Expiration Hosted Service is stopping.");

        await base.StopAsync(stoppingToken);
    }
}


