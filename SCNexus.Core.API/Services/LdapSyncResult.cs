namespace SCNexus.Core.API.Services
{
    /// <summary>Outcome of one Active Directory synchronisation run (manual or nightly).</summary>
    public sealed record LdapSyncResult(
        bool Success,
        string Message,
        int Processed,
        int Created,
        int Updated,
        int Deactivated,
        int Skipped,
        int Errors,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        string Trigger)
    {
        public double DurationSeconds => Math.Round((FinishedAt - StartedAt).TotalSeconds, 1);
    }

    /// <summary>Snapshot of the synchroniser state, returned by GET /api/Admin/sync-ldap/status.</summary>
    public sealed record LdapSyncStatus(
        bool IsRunning,
        DateTimeOffset? RunningSince,
        string? RunningTrigger,
        LdapSyncResult? LastResult,
        DateTimeOffset? NextScheduledRun);
}
