using Microsoft.AspNetCore.Identity;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Services
{
    /// <summary>
    /// Single entry point to run the AD synchronisation: serialises runs (one at a time), owns the
    /// service scope the sync needs (UserManager/DbContext are scoped, the runner is a singleton)
    /// and keeps the last result so the UI and the nightly job can report it.
    /// </summary>
    public sealed class LdapSyncRunner
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LdapSyncRunner> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _lock = new();

        private bool _isRunning;
        private DateTimeOffset? _runningSince;
        private string? _runningTrigger;
        private LdapSyncResult? _lastResult;
        private DateTimeOffset? _nextScheduledRun;

        public LdapSyncRunner(IServiceScopeFactory scopeFactory, ILogger<LdapSyncRunner> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public LdapSyncStatus Status
        {
            get
            {
                lock (_lock)
                {
                    return new LdapSyncStatus(_isRunning, _runningSince, _runningTrigger, _lastResult, _nextScheduledRun);
                }
            }
        }

        public void SetNextScheduledRun(DateTimeOffset? next)
        {
            lock (_lock) { _nextScheduledRun = next; }
        }

        /// <summary>Starts a run in the background. Returns false when one is already in progress.</summary>
        public bool TryStartInBackground(string trigger)
        {
            lock (_lock)
            {
                if (_isRunning) return false;
            }

            _ = Task.Run(() => RunAsync(trigger, CancellationToken.None));
            return true;
        }

        /// <summary>Runs the synchronisation now (waits if another run holds the gate).</summary>
        public async Task<LdapSyncResult> RunAsync(string trigger, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            lock (_lock)
            {
                _isRunning = true;
                _runningSince = DateTimeOffset.UtcNow;
                _runningTrigger = trigger;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<AdLdapSyncService>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var result = await sync.SyncUsersAsync(userManager, roleManager, db, trigger, cancellationToken);
                lock (_lock) { _lastResult = result; }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AD sync ({Trigger}) failed unexpectedly", trigger);
                var failed = new LdapSyncResult(false, $"Error inesperado: {ex.Message}", 0, 0, 0, 0, 0, 0,
                    _runningSince ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, trigger);
                lock (_lock) { _lastResult = failed; }
                return failed;
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                    _runningSince = null;
                    _runningTrigger = null;
                }
                _gate.Release();
            }
        }
    }
}
