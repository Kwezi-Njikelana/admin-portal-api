using System.ComponentModel.DataAnnotations;

namespace AdminPortalApi.Models;

public class CreateDepartmentDto
{
    [Required, StringLength(100)]
    public required string Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}
