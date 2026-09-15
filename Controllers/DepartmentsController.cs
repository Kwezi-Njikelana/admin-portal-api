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
    public class DepartmentsController : ControllerBase
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private const string AllDepartmentsCacheKey = "departments:all";

        private readonly ApplicationDbContext dbContext;
        private readonly IMemoryCache cache;
        private readonly ILogger<DepartmentsController> logger;

        public DepartmentsController(ApplicationDbContext dbContext, IMemoryCache cache, ILogger<DepartmentsController> logger)
        {
            this.dbContext = dbContext;
            this.cache = cache;
            this.logger = logger;
        }

        // GET: api/Departments
        [HttpGet]
        public async Task<IActionResult> GetAllDepartments()
        {
            if (!cache.TryGetValue(AllDepartmentsCacheKey, out List<Department>? departments))
            {
                departments = await dbContext.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
                cache.Set(AllDepartmentsCacheKey, departments, CacheDuration);
            }

            return Ok(departments);
        }

        // GET: api/Departments/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDepartmentById(int id)
        {
            var cacheKey = $"department:{id}";

            if (!cache.TryGetValue(cacheKey, out Department? department))
            {
                department = await dbContext.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
                if (department is not null)
                {
                    cache.Set(cacheKey, department, CacheDuration);
                }
            }

            if (department is null)
            {
                logger.LogWarning("Department {DepartmentId} not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Department not found", $"No department exists with id '{id}'."));
            }

            return Ok(department);
        }

        // GET: api/Departments/{id}/employees?page=1&pageSize=20
        [HttpGet("{id:int}/employees")]
        public async Task<IActionResult> GetDepartmentEmployees(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var departmentExists = await dbContext.Departments.AnyAsync(d => d.Id == id);
            if (!departmentExists)
            {
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Department not found", $"No department exists with id '{id}'."));
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var query = dbContext.Employees.AsNoTracking().Where(e => e.DepartmentId == id);

            var totalCount = await query.CountAsync();
            var employees = await query
                .OrderBy(e => e.LastName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PagedResult<Employee>
            {
                Items = employees,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        // POST: api/Departments
        [HttpPost]
        public async Task<IActionResult> CreateDepartment(CreateDepartmentDto createDepartmentDto)
        {
            var department = new Department
            {
                Name = createDepartmentDto.Name,
                Description = createDepartmentDto.Description
            };

            try
            {
                dbContext.Departments.Add(department);
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                logger.LogWarning(ex, "Attempted to create department with duplicate name {Name}", createDepartmentDto.Name);
                return Conflict(ProblemFor(StatusCodes.Status409Conflict, "Duplicate department name",
                    $"A department named '{createDepartmentDto.Name}' already exists."));
            }

            cache.Remove(AllDepartmentsCacheKey);
            logger.LogInformation("Created department {DepartmentId}", department.Id);
            return CreatedAtAction(nameof(GetDepartmentById), new { id = department.Id }, department);
        }

        // PUT: api/Departments/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateDepartment(int id, UpdateDepartmentDto updateDepartmentDto)
        {
            var department = await dbContext.Departments.FindAsync(id);
            if (department is null)
            {
                logger.LogWarning("Attempted to update department {DepartmentId} but it was not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Department not found", $"No department exists with id '{id}'."));
            }

            department.Name = updateDepartmentDto.Name;
            department.Description = updateDepartmentDto.Description;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                logger.LogWarning(ex, "Attempted to update department {DepartmentId} with duplicate name {Name}", id, updateDepartmentDto.Name);
                return Conflict(ProblemFor(StatusCodes.Status409Conflict, "Duplicate department name",
                    $"A department named '{updateDepartmentDto.Name}' already exists."));
            }

            cache.Remove(AllDepartmentsCacheKey);
            cache.Remove($"department:{id}");
            logger.LogInformation("Updated department {DepartmentId}", id);
            return Ok(department);
        }

        // DELETE: api/Departments/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var department = await dbContext.Departments.FindAsync(id);
            if (department is null)
            {
                logger.LogWarning("Attempted to delete department {DepartmentId} but it was not found", id);
                return NotFound(ProblemFor(StatusCodes.Status404NotFound, "Department not found", $"No department exists with id '{id}'."));
            }

            try
            {
                dbContext.Departments.Remove(department);
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
            {
                logger.LogWarning(ex, "Attempted to delete department {DepartmentId} while employees are still assigned to it", id);
                return Conflict(ProblemFor(StatusCodes.Status409Conflict, "Department in use",
                    "This department still has employees assigned to it. Reassign or remove them first."));
            }

            cache.Remove(AllDepartmentsCacheKey);
            cache.Remove($"department:{id}");
            logger.LogInformation("Deleted department {DepartmentId}", id);
            return NoContent();
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private static bool IsForeignKeyViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException pgEx &&
            (pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation || pgEx.SqlState == PostgresErrorCodes.RestrictViolation);

        private static ProblemDetails ProblemFor(int status, string title, string detail) => new()
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
