using System.ComponentModel.DataAnnotations.Schema;

namespace ConsoleApp1.DataAccess.Entities;

public class User
{
    public Guid Id { get; set; }
    public string? Login { get; set; } = null!;
    public string? Password { get; set; } = null!;
    public string? Nickname { get; set; } = null!;
    public string? Email { get; set; }
    [ForeignKey("UserId")]
    public List<Guid> Friends { get; set; } = new List<Guid>();
    public DateTime? BirthDate { get; set; }

}
