using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class TenantService : ITenantService
{
    
    private readonly GenericGraphQlService<Tenant>  _genericService;
    
    public TenantService(GraphQlClient client,
        ILogger<ContactService> logger,
        ILogger<GenericGraphQlService<Tenant>> genericLogger)
    { 
        _genericService = new GenericGraphQlService<Tenant>( client,  genericLogger, maxDepth: 4);
    }
    
    public async Task<SuccessResponse> ArchiveTenant(string contactId, bool archive, CancellationToken cancellationToken)
    {
        var tenant = await _genericService.Archive("Tenant",contactId,archive,cancellationToken);
        return new SuccessResponse
        {
            Data = tenant, Message = "Tenant archive successfully!", Success = true
        };
    }

    public async Task<Tenant> GetTenantById(string tenantId, CancellationToken cancellationToken)
    {
        return await _genericService.GetById("tenant","tenantId",tenantId,cancellationToken);
    }

    public async Task<Tenant> UpdateTenant(string tenantId, TenantInput tenantInput, CancellationToken cancellationToken)
    {
        return await _genericService.Update(
            "Tenant",
            "tenantId",
            "updateTenant",
            tenantId,
            tenantInput,
            cancellationToken
        );
    }

    public async Task<Tenant> CreateTenant(TenantInput tenantInput, CancellationToken cancellationToken)
    {
        return await _genericService.Create(
            "Tenant",
            "createTenant",
            "tenant",
            tenantInput,
            cancellationToken
        );

    }

    public async Task<TenantsPaginated> GetTenants(int limit, int skip, TenantFilter filters, CancellationToken cancellationToken)
    {
        var   userPaginated = await _genericService.GetPaginated<TenantsPaginated>(
            "tenants",
            "tenant",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return userPaginated;
    }
}