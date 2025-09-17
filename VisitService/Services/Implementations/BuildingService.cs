using VisitService.Helper;
using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class BuildingService: IBuildingService
{
    
    
    private readonly GenericGraphQlService<Building> _graphQlService;
    private readonly ILogger<BuildingService> _logger;
    private readonly GenericGraphQlService<Site> _siteService;

    public BuildingService(
        GraphQlClient client,
        ILogger<BuildingService> logger,
        ILogger<GenericGraphQlService<Building>> genericLogger, GenericGraphQlService<Site> siteService)
    {
        this._logger = logger;
        this._siteService = siteService;

        var limitSkipObjects = new List<LimitSkipObject>
        {
            new() { Name = "sites", Properties = new() { {"skip", 0}, {"limit", 1000} } },
            new() {Name = "SitesPaginated",Properties = new () { {"skip", 0}, {"limit", 1000} } }
        };
        
        _graphQlService = new GenericGraphQlService<Building>(
            client, 
            genericLogger,
            maxDepth: 4
            ,
            limitSkipObjects
            );
    }
    public async Task<Building> GetBuildingById(string buildingId, CancellationToken cancellationToken)
        => await _graphQlService.GetById( "building","buildingId", buildingId,cancellationToken);

    public async Task<SitesPaginated> GetSitesByBuildingId(string buildingId, int skip, int limit, CancellationToken cancellationToken)
    {
        var filters = new
        {
            buildingId = new { eq = buildingId }
        };
        var sitesPaginated = await _siteService.GetPaginated<SitesPaginated>(
            "sites",
            "site",
            filters,
            cancellationToken,
            skip,
            limit);
        
        return sitesPaginated;
    }

    public async Task<BuildingsPaginated> GetBuildings(BuildingFilter? buildingFilter = null, int skip = 0, int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await _graphQlService.GetPaginated<BuildingsPaginated>(
                "buildings",
                "building",
                buildingFilter,
                cancellationToken,
                skip,
                limit
            );
    }
}