using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Admin;

/// <summary>
/// Calcula en tiempo real los elementos pendientes de resolver en el área de gestión para administradores y moderadores (INC-149).
/// Sustituye cualquier contador estático o simulado en el menú de usuario.
/// </summary>
public class AdminPendingSummaryService : IAdminPendingSummaryService
{
    private readonly ISocialIngestionService _socialIngestionService;
    private readonly IGameIssueReportRepository _issueReportRepository;
    private readonly IWeeklyReleaseRepository _weeklyReleaseRepository;

    public AdminPendingSummaryService(
        ISocialIngestionService socialIngestionService,
        IGameIssueReportRepository issueReportRepository,
        IWeeklyReleaseRepository weeklyReleaseRepository)
    {
        _socialIngestionService = socialIngestionService ?? throw new ArgumentNullException(nameof(socialIngestionService));
        _issueReportRepository = issueReportRepository ?? throw new ArgumentNullException(nameof(issueReportRepository));
        _weeklyReleaseRepository = weeklyReleaseRepository ?? throw new ArgumentNullException(nameof(weeklyReleaseRepository));
    }

    public async Task<AdminPendingSummaryDto> GetPendingSummaryAsync(CancellationToken ct = default)
    {
        int socialPending = 0;
        int issueReportsPending = 0;
        int weeklyReleasesPending = 0;

        try
        {
            socialPending = await _socialIngestionService.GetPendingCountAsync(ct);
        }
        catch
        {
            socialPending = 0;
        }

        try
        {
            var reportSummary = await _issueReportRepository.GetSummaryAsync(ct);
            issueReportsPending = reportSummary.PendingCount + reportSummary.InReviewCount;
        }
        catch
        {
            issueReportsPending = 0;
        }

        try
        {
            var releases = await _weeklyReleaseRepository.GetReleasesAsync(null, ct);
            weeklyReleasesPending = releases.Count(r => r.Status == WeeklyReleaseStatus.PendingModeration);
        }
        catch
        {
            weeklyReleasesPending = 0;
        }

        int total = socialPending + issueReportsPending + weeklyReleasesPending;
        return new AdminPendingSummaryDto(
            PendingSocialInboxCount: socialPending,
            PendingIssueReportsCount: issueReportsPending,
            PendingWeeklyReleasesCount: weeklyReleasesPending,
            TotalPendingCount: total);
    }
}
