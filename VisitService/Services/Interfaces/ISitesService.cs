using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface ISitesService
{
    Task<SitesPaginated> GetSites(int skip, int limit, string buildingId, string modelType, string type, string parentSiteId, CancellationToken cancellationToken);
    Task<SitesPaginated> SearchSites(int? limit, int? skip, List<SiteFilter> filters, CancellationToken cancellationToken);
    Task<Site> GetSiteById(string siteId, CancellationToken cancellationToken);
}