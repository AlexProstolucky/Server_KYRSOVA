using System.ComponentModel.DataAnnotations.Schema;

namespace ConsoleApp1.DataAccess.Entities;

public enum TypeEnum
{
    Text,
    File,
    VoiceMessage,
    Call,
}
public class Message
{

    public Guid Id { get; set; }
    public TypeEnum Type { get; set; }
    public string Content { get; set; } = null!;
    [ForeignKey("FromId")]
    public User From { get; set; } = null!;
    public Guid FromId { get; set; }
    [ForeignKey("ToId")]
    public User To { get; set; } = null!;
    public Guid ToId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; } = DateTime.Now;
}
