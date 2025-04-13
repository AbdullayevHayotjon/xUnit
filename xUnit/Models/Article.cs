using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Article
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; }

    [Required]
    public string FilePath { get; set; } // Yuklangan fayl manzili

    [Required]
    public string Status { get; set; } = "Yuborilgan"; // Ko‘rilmoqda, Baholangan

    public string? ReviewComment { get; set; }

    public int? Grade { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    // FK: Student tomonidan yuklangan
    [ForeignKey("User")]
    public int StudentId { get; set; }
    public User Student { get; set; }
}
