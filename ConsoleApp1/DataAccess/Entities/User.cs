using System.ComponentModel.DataAnnotations.Schema;

namespace ConsoleApp1.DataAccess.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Login { get; set; }
    public string? Password { get; set; } = null!;
    public string? Nickname { get; set; } = null!;
    public string Email { get; set; }
    [ForeignKey("UserId")]
    public List<Guid>? Friends { get; set; } = new List<Guid>();
    [ForeignKey("UserId")]
    public List<Guid>? FriendsRequests { get; set; } = new List<Guid>();
    public DateTime? BirthDate { get; set; }

    public User(Guid id, string? login, string? password, string? nickname, string? email, DateTime? birthDate)
    {
        Id = id;
        Login = login ?? throw new ArgumentNullException(nameof(login));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
        Email = email;
        BirthDate = birthDate;
    }
}
