using System.Net.Http.Json;

namespace PhishGuard.Web;

public sealed class PhishGuardApiClient(HttpClient http)
{
    public Task<DatasetStats?> GetStatsAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<DatasetStats>("/api/stats", ct);

    public Task<DashboardDto?> GetDashboardAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<DashboardDto>("/api/dashboard", ct);

    public Task<PagedResult<CaseRow>?> GetCasesAsync(string? status, string? search, int page, int pageSize = 25, CancellationToken ct = default)
    {
        var q = $"/api/cases?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(status)) q += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrWhiteSpace(search)) q += $"&search={Uri.EscapeDataString(search)}";
        return http.GetFromJsonAsync<PagedResult<CaseRow>>(q, ct);
    }

    public Task<CaseDetailDto?> GetCaseAsync(long id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<CaseDetailDto>($"/api/cases/{id}", ct);

    public async Task InvestigateAsync(long id, CancellationToken ct = default) =>
        (await http.PostAsync($"/api/cases/{id}/investigate", null, ct)).EnsureSuccessStatusCode();

    public async Task AssessStepAsync(long id, int step, string level, string analyst, string? notes, CancellationToken ct = default) =>
        (await http.PostAsJsonAsync($"/api/cases/{id}/steps/{step}", new { level, analyst, notes }, ct)).EnsureSuccessStatusCode();

    public async Task DecideAsync(long id, string decision, string analyst, string? notes, CancellationToken ct = default) =>
        (await http.PostAsJsonAsync($"/api/cases/{id}/decision", new { decision, analyst, notes }, ct)).EnsureSuccessStatusCode();

    public Task<PagedResult<NoticeDto>?> GetNoticesAsync(string? status, int page, int pageSize = 25, CancellationToken ct = default) =>
        http.GetFromJsonAsync<PagedResult<NoticeDto>>($"/api/notices?page={page}&pageSize={pageSize}" + (string.IsNullOrEmpty(status) ? "" : $"&status={status}"), ct);

    public async Task ApproveNoticeAsync(long id, string analyst, CancellationToken ct = default) =>
        (await http.PostAsJsonAsync($"/api/notices/{id}/approve", new { analyst }, ct)).EnsureSuccessStatusCode();

    public async Task RejectNoticeAsync(long id, string analyst, CancellationToken ct = default) =>
        (await http.PostAsJsonAsync($"/api/notices/{id}/reject", new { analyst }, ct)).EnsureSuccessStatusCode();

    public Task<PagedResult<IndicatorDto>?> GetIndicatorsAsync(string? type, int page, int pageSize = 50, CancellationToken ct = default) =>
        http.GetFromJsonAsync<PagedResult<IndicatorDto>>($"/api/indicators?page={page}&pageSize={pageSize}" + (string.IsNullOrEmpty(type) ? "" : $"&type={type}"), ct);

    public Task<byte[]> GetBlocklistCsvAsync(CancellationToken ct = default) =>
        http.GetByteArrayAsync("/api/indicators?format=csv", ct);
}
