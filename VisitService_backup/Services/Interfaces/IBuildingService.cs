using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface IBuildingService
{
    Task<Building> GetBuildingById(string buildingId, CancellationToken cancellationToken);
    Task<SitesPaginated> GetSitesByBuildingId(string buildingId, int skip, int limit, CancellationToken cancellationToken);
    Task<BuildingsPaginated> GetBuildings(BuildingFilter? buildingFilter = null, int skip = 0, int limit = 20, CancellationToken cancellationToken = default);
}