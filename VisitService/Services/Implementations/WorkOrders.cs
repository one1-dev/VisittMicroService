using VisitService.Helper;
using VisitService.Models;
using VisitService.Repos;

namespace VisitService.Services.Implementations;

public class WorkOrdersService: IWorkOrdersService
{
    private readonly GenericGraphQlService<WorkOrder> genericService;
    private readonly ILogger<WorkOrdersService> logger;

    public WorkOrdersService(
        GraphQlClient client,
        ILogger<WorkOrdersService> logger,
        ILogger<GenericGraphQlService<WorkOrder>> genericLogger)
    {
        this.logger = logger;
        
        var limitSkipObjects = new List<LimitSkipObject>
        {
            new() { Name = "sites", Properties = new() { {"skip", 0}, {"limit", 20} } }
        };
        
        genericService = new GenericGraphQlService<WorkOrder>(
            client, 
            genericLogger, 
            maxDepth: 2,
            limitSkipObjects);
    }
    public async Task<List<WorkOrder>> WorkOrdersBulkPut(WorkOrdersBulkPutRequest request, CancellationToken cancellationToken)
    {
        var variables = new 
        {
            workOrderIds = request.WorkOrderIds,
            input = request.Input
        };
        
        return await genericService.ExecuteBulkUpdate(
            "WorkOrder",
            "updateWorkOrders", 
            "workOrders",
            variables, 
            cancellationToken
        );
    }
    
    public async Task<WorkOrder> WorkOrdersPost(WorkOrderInput workOrderInput, CancellationToken cancellationToken)
    {
        return await genericService.Create(
            "WorkOrder",
            "createWorkOrder",
            "workOrder",
            workOrderInput,
            cancellationToken
        );
    }
    
    public async Task<WorkOrder> GetWorkOrderByIdGet(string workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await genericService.GetById(
            "workOrder",
            "workOrderId",
            workOrderId,
            cancellationToken);
        return workOrder;
    }

    public async Task<WorkOrder> WorkOrdersWorkOrderIdPut(string workOrderId, WorkOrderUpdateInput workOrderUpdateInput,
        CancellationToken cancellationToken)
    {
        return await genericService.Update(
            "WorkOrder",
            "updateWorkOrder",
            workOrderId,
            workOrderUpdateInput,
            cancellationToken
        );
    }

    public async Task<WorkOrdersPaginated> WorkOrdersGet(WorkOrderFilter? filters, CancellationToken cancellationToken,
        int skip = 0, int limit = 20)
    {
        
        var gResponse =  await genericService.GetPaginated<WorkOrdersPaginated>(
            "getWorkOrders",
            filters,
            cancellationToken,
            skip,
            limit
        );
        return gResponse;
    }
    
    public async Task<WorkOrder> CancelWorkOrder(
        string workOrderId,
        CancellationToken cancellationToken) 
    {
        logger.LogInformation("Starting cancellation of work order {WorkOrderId}", workOrderId);
        
        var charges = await genericService.GetPaginated<ChargesPaginated>(
            "charges",
            null,
            cancellationToken);
        
        var workOrderCharges = charges.Items
            .Where(c => c.WorkOrder?.Id == workOrderId)
            .ToList();
        
        if (workOrderCharges.Any())
        {
            logger.LogWarning("Work order {WorkOrderId} has {ChargeCount} charges - cannot cancel", 
                workOrderId, workOrderCharges.Count);
            
            throw new InvalidOperationException(
                $"Cannot cancel work order {workOrderId} - it has {workOrderCharges.Count} charges. " +
                "Please handle charges first.");
        }

        logger.LogInformation("No charges found for work order {WorkOrderId}, proceeding with cancellation", workOrderId);

        var deletedTime = DateTime.UtcNow;
        var updateInput = new WorkOrderUpdateInput
        {
            Status = WorkOrderStatus.CanceledEnum,
            StatusText = $"Deleted via API at {deletedTime:yyyy-MM-dd HH:mm:ss} UTC"
        };

        var variables = new 
        {
            workOrderIds = new[] { workOrderId },
            input = updateInput
        };

 
        var result = await genericService.ExecuteBulkUpdate(
            "WorkOrder",
            "updateWorkOrders", 
            "workOrders",
            variables, 
            cancellationToken);

        if (result?.Any() == true)
        {
            logger.LogInformation("Successfully canceled work order {WorkOrderId}", workOrderId);
            return result.First();
        }
        
        logger.LogWarning("No result returned when canceling work order {WorkOrderId}", workOrderId);
        return null;
        
    }
}