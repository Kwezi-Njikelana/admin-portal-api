namespace AdminPortal.Api.Models;

public class UpdateEmployeeDto
{
     public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal Salary { get; set; }
}
