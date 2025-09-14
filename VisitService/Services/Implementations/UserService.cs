using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class UserService: IUserService
{
    private readonly GenericGraphQlService<User>  _genericService;
    public UserService(GraphQlClient client,
        ILogger<UserService> logger,
        ILogger<GenericGraphQlService<User>> genericLogger)
    { 
        _genericService = new GenericGraphQlService<User>( client,  genericLogger, maxDepth: 3);
    }
    
    public Task<UsersPaginated> GetUsers(int skip, int limit, UserFilter filters, CancellationToken cancellationToken)
    {
        var userPaginated = _genericService.GetPaginated<UsersPaginated>(
            "users",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return userPaginated;
    }

    public async Task<User> GetUserById(string userId, CancellationToken cancellationToken)
    {
        return await _genericService.GetById(
            "user",
            "userId",
            userId,
            cancellationToken);
    }
}