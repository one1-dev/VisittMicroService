#nullable enable
using MPACKAGE.LibDomain.Extensions;
using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class SitesService: ISitesService
{
    private readonly GenericGraphQlService<Site>  _genericService;
    public SitesService(GraphQlClient client,
        ILogger<SitesService> logger,
        ILogger<GenericGraphQlService<Site>> genericLogger)
    {
        _genericService = new GenericGraphQlService<Site>(
            client, 
            genericLogger, 
            maxDepth: 3);
    }


    public async Task<SitesPaginated> GetSites(int skip, int limit, string buildingId, string modelType, string type, string parentSiteId,
        CancellationToken cancellationToken)
    {
  
    var filters = BuildFilters(buildingId, modelType, type, parentSiteId);
    
    var sitesPaginated = await _genericService.GetPaginated<SitesPaginated>(
            "sites",
            "site",
            filters,
            cancellationToken,
            skip,
            limit);
        
        return sitesPaginated;
    }

    public async Task<SitesPaginated> SearchSites(int? limit, int? skip, List<SiteFilter> filters, CancellationToken cancellationToken)
    {
        return await _genericService.GetPaginated<SitesPaginated>(
            "sites",
            "site",
            filters,
            cancellationToken,
            skip ??= 0,
            limit ??= 20
        );
    }

    public async Task<Site> GetSiteById(string siteId, CancellationToken cancellationToken)
    {
        return await _genericService.GetById(
            "site" ,
            "siteId",
            siteId,
            cancellationToken
            );
    }

    private static List<SiteFilter> BuildFilters(
        string? buildingId, 
        string? modelType, 
        string? type, 
        string? parentSiteId)
    {
        var filters = new List<SiteFilter>();
        var filter = new SiteFilter();
        var hasFilters = false;
        
        if (!string.IsNullOrEmpty(buildingId))
        {
            filter.BuildingId = new FilterIdStringArray { Eq = buildingId };
            hasFilters = true;
        }
        if (!string.IsNullOrEmpty(modelType))
        {
            filter.ModelType = new FilterSiteModelType { Eq = ParseModelTypeToEnum(modelType)};
            
            hasFilters = true;
        }
        if (!string.IsNullOrEmpty(type))
        {
            filter.Type = new FilterStringArray { Eq = type };
            hasFilters = true;
        }
        
        if (!string.IsNullOrEmpty(parentSiteId))
        {
            filter.ParentSiteId = new FilterIdStringArray { Eq = parentSiteId };
            hasFilters = true;
        }
        
        if (hasFilters)
            filters.Add(filter);
            
        return filters;
    }
    
    private static FilterSiteModelType.EqEnum ParseModelTypeToEnum(string? modelType)
    {
        if (string.IsNullOrEmpty(modelType))
            throw new ArgumentNullException(nameof(modelType));
        
        return modelType.ToLowerInvariant() switch
        {
            "site" => FilterSiteModelType.EqEnum.SiteEnum,
            "equipment" => FilterSiteModelType.EqEnum.EquipmentEnum,
            "leasable_site" => FilterSiteModelType.EqEnum.LeasableSiteEnum,
            "floor" => FilterSiteModelType.EqEnum.FloorEnum,
            _ => throw new ArgumentException($"Invalid modelType: '{modelType}'. Valid values: site, equipment, leasable_site, floor", nameof(modelType))
        };
    }
    
}