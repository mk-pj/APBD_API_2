using Tutorial9.Model;

namespace Tutorial9.Services;

public interface IDbService
{
    Task<bool> CheckIfProductExists(int idProduct);
    Task<bool> CheckIfWarehouseExists(int idWarehouse);
    Task<bool> ValidateOrder(int idProduct, int amount, DateTime createDate);

    Task<bool> IsOrderAlreadyFulfilledAsync(int idOrder);

    Task<int> GetOrderId(int idProduct, int amount, DateTime createdDate);

    Task SetFulfilledAtDate(int orderId);

    Task<int> InsertIntoProductWarehouse(ProductWarehouseDto dto, int orderId);

    Task<int> CallProcedure(ProductWarehouseDto dto);

}