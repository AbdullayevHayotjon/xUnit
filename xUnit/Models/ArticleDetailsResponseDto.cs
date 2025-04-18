namespace ArticleForDT.Models
{
    public class ArticleDetailsResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string FileUrl { get; set; }
        public string Status { get; set; }
        public int? Grade { get; set; }
        public string ReviewComment { get; set; }
        public DateTime UploadDate { get; set; }
        public StudentResponseDto Student { get; set; }
    }
}
