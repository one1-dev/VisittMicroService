using VisitService.Models;
using VisitService.Repos;
using VisitService.Services.Interfaces;

namespace VisitService.Services.Implementations;

public class ContactService : IContactService
{
    private readonly GenericGraphQlService<Contact>  _genericService;
    
    public ContactService(GraphQlClient client,
        ILogger<ContactService> logger,
        ILogger<GenericGraphQlService<Contact>> genericLogger)
    { 
        _genericService = new GenericGraphQlService<Contact>( client,  genericLogger, maxDepth: 3);
    }
    
    public Task<SuccessResponse> DeleteContact(string contactId, bool archive, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Contact> GetContactById(string contactId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Contact> UpdateContact(string contactId, ContactInput contactInput, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<Contact> CreateContact(ContactInput contactInput, CancellationToken cancellationToken)
    {
        return await _genericService.Create(
            "contact",
            "createContact",
            "contact",
            contactInput,
            cancellationToken
        );

    }

    public async Task<ContactsPaginated> GetContacts(int limit, int skip, ContactFilter filters, CancellationToken cancellationToken)
    {
        var   userPaginated = await _genericService.GetPaginated<ContactsPaginated>(
            "contacts",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return userPaginated;
    }
}