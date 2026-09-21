using System.ComponentModel.DataAnnotations;

namespace AdminPortalApi.Models;

public class CreateEmployeeDto
{
    [Required, StringLength(100)]
    public required string FirstName { get; set; }

    [Required, StringLength(100)]
    public required string LastName { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    public DateTime HireDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Salary cannot be negative.")]
    public decimal Salary { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "DepartmentId must reference an existing department.")]
    public int DepartmentId { get; set; }
}
