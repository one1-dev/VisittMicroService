using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface ICategoriesService
{
    Task<Category> GetCategoryById(string categoryId, CancellationToken cancellationToken);
    Task<CategoriesPaginated> GetCategories(int limit, int skip, CategoryFilter filters, CancellationToken cancellationToken);
}