using Microsoft.AspNetCore.Mvc;
using Tutorial9.Middlewares;
using Tutorial9.Model;
using Tutorial9.Services;

namespace Tutorial9.Controllers;


[ApiController]
[Route("api/[controller]")]
public class WarehouseController(IDbService dbService) : ControllerBase
{

    private readonly IDbService _dbService = dbService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductWarehouseDto dto)
    {
        if (!await _dbService.CheckIfProductExists(dto.IdProduct))
            throw new NotFoundException("Product not found");

        if (dto.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero");

        if (!await _dbService.CheckIfWarehouseExists(dto.IdWarehouse))
            throw new NotFoundException("Warehouse not found");

        if (!await _dbService.ValidateOrder(dto.IdProduct, dto.Amount, dto.CreatedAt))
            throw new ArgumentException("No valid order found");

        var orderId = await _dbService.GetOrderId(dto.IdProduct, dto.Amount, dto.CreatedAt);

        if (await _dbService.IsOrderAlreadyFulfilledAsync(orderId))
            throw new ConflictException("Order already fulfilled");

        await _dbService.SetFulfilledAtDate(orderId);

        var id = await _dbService.InsertIntoProductWarehouse(dto, orderId);

        return Ok(id);
    }
    
    
    [HttpPost("procedure")]
    public async Task<IActionResult> CreateFromProcedure([FromBody] ProductWarehouseDto dto)
    {
        if(dto.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero");
        var id = await _dbService.CallProcedure(dto);

        return Ok(id);
    }
    
}