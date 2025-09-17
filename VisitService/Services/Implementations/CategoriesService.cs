using Newtonsoft.Json;
using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class CategoriesService: ICategoriesService
{
    private readonly GenericGraphQlService<Category>  _genericService;
    private readonly ILogger<CategoriesService> _logger;
    
    public CategoriesService(GraphQlClient client,
        ILogger<CategoriesService> logger,
        ILogger<GenericGraphQlService<Category>> genericLogger)
    { 
        _genericService = new GenericGraphQlService<Category>( client,  genericLogger, maxDepth: 4);
        _logger = logger;
    }
    
    public async Task<Category> GetCategoryById(string categoryId, CancellationToken cancellationToken)
        => await _genericService.GetById("category","categoryId",categoryId,cancellationToken);


    public async Task<CategoriesPaginated> GetCategories(int limit, int skip, CategoryFilter filters, CancellationToken cancellationToken)
    {
        var json = JsonConvert.SerializeObject(filters, new JsonSerializerSettings 
        { 
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), 
            NullValueHandling = NullValueHandling.Ignore 
        });
        _logger.LogInformation("filter  json is {json}", json);
        var   userPaginated = await _genericService.GetPaginated<CategoriesPaginated>(
            "categories",
            "category",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return userPaginated;
    }
}