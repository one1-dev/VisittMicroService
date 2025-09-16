using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface ITenantService
{
    Task<SuccessResponse> ArchiveTenant(string tenantId, bool archive, CancellationToken cancellationToken);
    Task<Tenant> GetTenantById(string tenantId, CancellationToken cancellationToken);
    Task<Tenant> UpdateTenant(string tenantId, TenantInput tenantInput, CancellationToken cancellationToken);
    Task<Tenant> CreateTenant(TenantInput tenantInput, CancellationToken cancellationToken);
    Task<TenantsPaginated> GetTenants(int limit, int skip, TenantFilter filters, CancellationToken cancellationToken);
}