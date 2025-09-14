using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface IUserService
{
    Task<UsersPaginated> GetUsers(int skip, int limit, UserFilter filters, CancellationToken cancellationToken);
    Task<User>  GetUserById(string userId,CancellationToken cancellationToken);
}