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
        _genericService = new GenericGraphQlService<Contact>( client,  genericLogger, maxDepth: 4);
    }
    
    public async Task<SuccessResponse> DeleteContact(string contactId, bool archive, CancellationToken cancellationToken)
    {
        var contact = await _genericService.Archive("Contact",contactId,archive,cancellationToken);
        return new SuccessResponse
        {
            Data = contact, Message = "Contact archive successfully!", Success = true
        };
    }

    public async Task<Contact> GetContactById(string contactId, CancellationToken cancellationToken)
    {
        return await _genericService.GetById("contact","contactId",contactId,cancellationToken);
    }

    public async Task<Contact> UpdateContact(string contactId, ContactInput contactInput, CancellationToken cancellationToken)
    {
        return await _genericService.Update(
            "Contact",
            "contactId",
            "updateContact",
            contactId,
            contactInput,
            cancellationToken
        );
    }

    public async Task<Contact> CreateContact(ContactInput contactInput, CancellationToken cancellationToken)
    {
        return await _genericService.Create(
            "Contact",
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
            "contact",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return userPaginated;
    }
}