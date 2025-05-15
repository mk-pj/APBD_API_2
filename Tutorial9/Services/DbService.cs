using System.Data;
using Microsoft.Data.SqlClient;
using Tutorial9.Middlewares;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class DbService(IConfiguration configuration) : IDbService
{
    
    private readonly string _connectionString = configuration.GetConnectionString("Default") ?? string.Empty;

    public async Task<bool> CheckIfProductExists(int idProduct)
    {
        const string query = "SELECT 1 FROM Product WHERE Product.IdProduct = @IdProduct;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        
        var res = await command.ExecuteScalarAsync();
        return res != null;
    }
    
    public async Task<bool> CheckIfWarehouseExists(int idWarehouse)
    {
        const string query = "SELECT 1 FROM Warehouse WHERE Warehouse.IdWarehouse = @IdWarehouse;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdWarehouse", idWarehouse);
        
        var res = await command.ExecuteScalarAsync();
        return res != null;
    }
    
    public async Task<bool> ValidateOrder(int idProduct, int amount, DateTime createDate)
    {
        const string query = @"
            SELECT o.CreatedAt FROM [Order] o
            WHERE o.IdProduct = @IdProduct AND o.Amount = @Amount;";
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        command.Parameters.AddWithValue("@Amount", amount);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            DateTime orderDate = reader.GetDateTime(0);
            if (orderDate < createDate)
                return true;
        }
        return false;
    }
    
    public async Task<bool> IsOrderAlreadyFulfilledAsync(int idOrder)
    {
        const string query = "SELECT 1 FROM Product_Warehouse WHERE IdOrder = @IdOrder;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdOrder", idOrder);

        var result = await command.ExecuteScalarAsync();
        return result != null;
    }

    public async Task SetFulfilledAtDate(int orderId)
    {
        const string query = @"
            UPDATE [Order]
            SET [Order].FulfilledAt = GETDATE()
            WHERE [Order].IdOrder = @IdOrder;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdOrder", orderId);
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task<int> GetOrderId(int idProduct, int amount, DateTime createdDate)
    {
        const string query = @"
            SELECT o.IdOrder FROM [Order] o
            WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt = @CreatedAt;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        command.Parameters.AddWithValue("@Amount", amount);
        command.Parameters.AddWithValue("@CreatedAt", createdDate);
        
        var reader = await command.ExecuteReaderAsync();
        
        int? id  = null;
        if(await reader.ReadAsync())
            id = reader.GetInt32(0);
        return id ?? throw new NotFoundException($"No order found for Product: {idProduct}");
    }
    
    private async Task<decimal> GetProductPrice(int idProduct)
    {
        const string query = "SELECT Price FROM Product WHERE IdProduct = @IdProduct;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdProduct", idProduct);
        
        var reader = await command.ExecuteReaderAsync();
        
        decimal? price = null;
        if(await reader.ReadAsync())
            price = reader.GetDecimal(0);
        return price ?? throw new NotFoundException($"Product {idProduct} not found when fetching price");
    }

    public async Task<int> InsertIntoProductWarehouse(ProductWarehouseDto dto, int orderId)
    {
        const string query = @"
            INSERT INTO Product_Warehouse(IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
            VALUES(@IdWarehouse, @IdProduct, @IdOrder,@Amount, @Price, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(query, connection, connection.BeginTransaction());
        try
        {
            var price = await GetProductPrice(dto.IdProduct);

            command.Parameters.AddWithValue("@IdWarehouse", dto.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", dto.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", orderId);
            command.Parameters.AddWithValue("@Amount", dto.Amount);
            command.Parameters.AddWithValue("@Price", price * dto.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            
            var id = await command.ExecuteScalarAsync();
            await command.Transaction.CommitAsync();
            return Convert.ToInt32(id);
        }
        catch (Exception e)
        {
            await command.Transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> CallProcedure(ProductWarehouseDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("AddProductToWarehouse ", connection);
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@IdWarehouse", dto.IdWarehouse);
        command.Parameters.AddWithValue("@IdProduct", dto.IdProduct);
        command.Parameters.AddWithValue("@Amount", dto.Amount);
        command.Parameters.AddWithValue("@CreatedAt", dto.CreatedAt);

        try
        {
            var res = await command.ExecuteScalarAsync();
            return Convert.ToInt32(res);
        }
        catch (Exception e)
        {
            throw new ArgumentException(e.Message);
        }
    }

    // public async Task DoSomethingAsync()
    // {
    //     await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
    //     await using SqlCommand command = new SqlCommand();
    //     
    //     command.Connection = connection;
    //     await connection.OpenAsync();
    //
    //     DbTransaction transaction = await connection.BeginTransactionAsync();
    //     command.Transaction = transaction as SqlTransaction;
    //
    //     // BEGIN TRANSACTION
    //     try
    //     {
    //         command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
    //         command.Parameters.AddWithValue("@IdAnimal", 1);
    //         command.Parameters.AddWithValue("@Name", "Animal1");
    //     
    //         await command.ExecuteNonQueryAsync();
    //     
    //         command.Parameters.Clear();
    //         command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
    //         command.Parameters.AddWithValue("@IdAnimal", 2);
    //         command.Parameters.AddWithValue("@Name", "Animal2");
    //     
    //         await command.ExecuteNonQueryAsync();
    //         
    //         await transaction.CommitAsync();
    //     }
    //     catch (Exception e)
    //     {
    //         await transaction.RollbackAsync();
    //         throw;
    //     }
    //     // END TRANSACTION
    // }
    //
    // public async Task ProcedureAsync()
    // {
    //     await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
    //     await using SqlCommand command = new SqlCommand();
    //     
    //     command.Connection = connection;
    //     await connection.OpenAsync();
    //     
    //     command.CommandText = "NazwaProcedury";
    //     command.CommandType = CommandType.StoredProcedure;
    //     
    //     command.Parameters.AddWithValue("@Id", 2);
    //     
    //     await command.ExecuteNonQueryAsync();
    //     
    // }
}