using VisitService.Models;

namespace VisitService.Services.Interfaces;

public interface IContactService
{
    Task<SuccessResponse> DeleteContact(string contactId, bool archive, CancellationToken cancellationToken);

    Task<Contact> GetContactById(string contactId, CancellationToken cancellationToken);


    Task<Contact> UpdateContact(string contactId, ContactInput contactInput, CancellationToken cancellationToken);
    Task<Contact> CreateContact(ContactInput contactInput, CancellationToken cancellationToken);
    Task<ContactsPaginated> GetContacts(int limit, int skip, ContactFilter filters, CancellationToken cancellationToken);
}
    
    