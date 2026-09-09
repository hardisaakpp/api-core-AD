namespace SCNexus.Core.API.Services
{
    /// <summary>
    /// Runs the Active Directory user synchronisation once a night, inside the Core itself (no
    /// external scheduler). Configuration (section Ldap:NightlySync):
    ///   Enabled   - default true.
    ///   Time      - local wall-clock time "HH:mm", default "02:30".
    ///   TimeZone  - IANA/Windows id for that time, default "America/Guayaquil" (falls back to UTC-5).
    ///   DeactivateMissing - read by AdLdapSyncService; default false (never deactivates on its own).
    /// </summary>
    public sealed class AdUserNightlySyncHostedService : BackgroundService
    {
        private readonly LdapSyncRunner _runner;
        private readonly IConfiguration _config;
        private readonly ILogger<AdUserNightlySyncHostedService> _logger;

        public AdUserNightlySyncHostedService(LdapSyncRunner runner, IConfiguration config, ILogger<AdUserNightlySyncHostedService> logger)
        {
            _runner = runner;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = !bool.TryParse(_config["Ldap:NightlySync:Enabled"], out var e) || e;
            if (!enabled)
            {
                _logger.LogInformation("AD nightly sync disabled (Ldap:NightlySync:Enabled=false)");
                return;
            }

            if (string.IsNullOrWhiteSpace(_config["Ldap:Host"]))
            {
                _logger.LogWarning("AD nightly sync not scheduled: Ldap:Host is not configured");
                return;
            }

            var timeOfDay = ParseTime(_config["Ldap:NightlySync:Time"]);
            var timeZone = ResolveTimeZone(_config["Ldap:NightlySync:TimeZone"]);
            _logger.LogInformation("AD nightly sync scheduled every day at {Time} ({TimeZone})", timeOfDay, timeZone.Id);

            while (!stoppingToken.IsCancellationRequested)
            {
                var next = NextRun(DateTimeOffset.UtcNow, timeOfDay, timeZone);
                _runner.SetNextScheduledRun(next);
                _logger.LogInformation("AD nightly sync: next run at {Next:u}", next);

                try
                {
                    await DelayUntil(next, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    var result = await _runner.RunAsync("nightly", stoppingToken);
                    _logger.Log(result.Success ? LogLevel.Information : LogLevel.Warning,
                        "AD nightly sync finished in {Seconds}s: {Message}", result.DurationSeconds, result.Message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AD nightly sync failed");
                }
            }

            _runner.SetNextScheduledRun(null);
        }

        private static TimeSpan ParseTime(string? value)
            => TimeSpan.TryParseExact(value?.Trim(), @"hh\:mm", null, out var t) ? t : new TimeSpan(2, 30, 0);

        private TimeZoneInfo ResolveTimeZone(string? id)
        {
            var candidates = new[] { id, "America/Guayaquil", "SA Pacific Standard Time" }
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim());

            foreach (var candidate in candidates)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(candidate);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException)
                {
                    _logger.LogWarning("Time zone '{Id}' not available on this host", candidate);
                }
            }

            // Ecuador has no DST, so a fixed offset is an exact fallback for containers without tzdata.
            return TimeZoneInfo.CreateCustomTimeZone("UTC-05", TimeSpan.FromHours(-5), "UTC-05", "UTC-05");
        }

        internal static DateTimeOffset NextRun(DateTimeOffset nowUtc, TimeSpan timeOfDay, TimeZoneInfo timeZone)
        {
            var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
            var candidateLocal = localNow.Date + timeOfDay;
            if (candidateLocal <= localNow.DateTime)
            {
                candidateLocal = candidateLocal.AddDays(1);
            }

            var offset = timeZone.GetUtcOffset(candidateLocal);
            return new DateTimeOffset(candidateLocal, offset).ToUniversalTime();
        }

        private static async Task DelayUntil(DateTimeOffset target, CancellationToken token)
        {
            // Sleep in slices so a long wait survives clock adjustments and stays cancellable.
            while (true)
            {
                var remaining = target - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) return;
                await Task.Delay(remaining > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : remaining, token);
            }
        }
    }
}
