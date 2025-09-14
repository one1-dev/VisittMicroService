using VisitService.Models;

namespace VisitService.Services;

public interface IWorkOrdersService
{
    Task<List<WorkOrder>> WorkOrdersBulkPut(WorkOrdersBulkPutRequest workOrdersBulkPutRequest, CancellationToken cancellationToken);
    Task<WorkOrder> WorkOrdersPost(WorkOrderInput workOrderInput, CancellationToken cancellationToken);
    Task<WorkOrder> GetWorkOrderByIdGet(string workOrderId, CancellationToken cancellationToken);
    Task<WorkOrder> WorkOrdersWorkOrderIdPut(string workOrderId, WorkOrderUpdateInput workOrderUpdateInput, CancellationToken cancellationToken);
    Task<WorkOrdersPaginated> WorkOrdersGet( WorkOrderFilter? filters, CancellationToken cancellationToken, int skip = 0, int limit = 20);
}