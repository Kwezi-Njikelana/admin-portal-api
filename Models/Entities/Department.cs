namespace AdminPortal.Models.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
