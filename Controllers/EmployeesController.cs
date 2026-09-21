using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using AdminPortalApi.Data;
using AdminPortalApi.Models;
using AdminPortal.Models.Entities;

namespace AdminPortalApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache cache;
        private readonly ILogger<EmployeesController> logger;

        public EmployeesController(ApplicationDbContext dbContext, IMemoryCache cache, ILogger<EmployeesController> logger)
        {
            this.dbContext = dbContext;
            this.cache = cache;
            this.logger = logger;
        }

        // GET: api/Employees?search=&departmentId=&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetAllEmployees(
            [FromQuery] string? search,
            [FromQuery] int? departmentId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var query = dbContext.Employees.AsNoTracking().AsQueryable();

            if (departmentId is not null)
            {
                query = query.Where(e => e.DepartmentId == departmentId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.ILike(e.FirstName, term) ||
                    EF.Functions.ILike(e.LastName, term) ||
                    EF.Functions.ILike(e.Email, term));
            }

            var totalCount = await query.CountAsync();
            var employees = await query
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            logger.LogInformation("Retrieved {Count} of {TotalCount} employees (page {Page})", employees.Count, totalCount, page);

            return Ok(new PagedResult<Employee>
            {
                Items = employees,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        // GET: api/Employees/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetEmployeeById(Guid id)
        {
            var cacheKey = $"employee:{id}";

            if (!cache.TryGetValue(cacheKey, out Employee? employee))
            {
                employee = await dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (employee is not null)
                {
                    cache.Set(cacheKey, employee, CacheDuration);
                }
            }

            if (employee is null)
            {
                logger.LogWarning("Employee {EmployeeId} not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Employee not found", $"No employee exists with id '{id}'."));
            }

            return Ok(employee);
        }

        // POST: api/Employees
        [HttpPost]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeDto createEmployeeDto)
        {
            var departmentExists = await dbContext.Departments.AnyAsync(d => d.Id == createEmployeeDto.DepartmentId);
            if (!departmentExists)
            {
                return BadRequest(ProblemFor(StatusCodes.Status400BadRequest, "Invalid department",
                    $"Department with id '{createEmployeeDto.DepartmentId}' does not exist."));
            }

            var employee = new Employee
            {
                FirstName = createEmployeeDto.FirstName,
                LastName = createEmployeeDto.LastName,
                Email = createEmployeeDto.Email,
                PhoneNumber = createEmployeeDto.PhoneNumber,
                HireDate = createEmployeeDto.HireDate,
                Salary = createEmployeeDto.Salary,
                DepartmentId = createEmployeeDto.DepartmentId
            };

            try
            {
                dbContext.Employees.Add(employee);
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                logger.LogWarning(ex, "Attempted to create employee with duplicate email {Email}", createEmployeeDto.Email);
                return Conflict(ProblemFor(StatusCodes.Status409Conflict, "Duplicate email",
                    $"An employee with email '{createEmployeeDto.Email}' already exists."));
            }

            logger.LogInformation("Created employee {EmployeeId}", employee.Id);
            return CreatedAtAction(nameof(GetEmployeeById), new { id = employee.Id }, employee);
        }

        // PUT: api/Employees/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateEmployee(Guid id, UpdateEmployeeDto updateEmployeeDto)
        {
            var employee = await dbContext.Employees.FindAsync(id);
            if (employee is null)
            {
                logger.LogWarning("Attempted to update employee {EmployeeId} but it was not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Employee not found", $"No employee exists with id '{id}'."));
            }

            var departmentExists = await dbContext.Departments.AnyAsync(d => d.Id == updateEmployeeDto.DepartmentId);
            if (!departmentExists)
            {
                return BadRequest(ProblemFor(StatusCodes.Status400BadRequest, "Invalid department",
                    $"Department with id '{updateEmployeeDto.DepartmentId}' does not exist."));
            }

            employee.FirstName = updateEmployeeDto.FirstName;
            employee.LastName = updateEmployeeDto.LastName;
            employee.Email = updateEmployeeDto.Email;
            employee.PhoneNumber = updateEmployeeDto.PhoneNumber;
            employee.Salary = updateEmployeeDto.Salary;
            employee.DepartmentId = updateEmployeeDto.DepartmentId;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                logger.LogWarning(ex, "Attempted to update employee {EmployeeId} with duplicate email {Email}", id, updateEmployeeDto.Email);
                return Conflict(ProblemFor(StatusCodes.Status409Conflict, "Duplicate email",
                    $"An employee with email '{updateEmployeeDto.Email}' already exists."));
            }

            cache.Remove($"employee:{id}");
            logger.LogInformation("Updated employee {EmployeeId}", id);
            return Ok(employee);
        }

        // DELETE: api/Employees/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteEmployee(Guid id)
        {
            var employee = await dbContext.Employees.FindAsync(id);
            if (employee is null)
            {
                logger.LogWarning("Attempted to delete employee {EmployeeId} but it was not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Employee not found", $"No employee exists with id '{id}'."));
            }

            dbContext.Employees.Remove(employee);
            await dbContext.SaveChangesAsync();

            cache.Remove($"employee:{id}");
            logger.LogInformation("Deleted employee {EmployeeId}", id);
            return NoContent();
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private static ProblemDetails ProblemFor(int status, string title, string detail) => new()
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
