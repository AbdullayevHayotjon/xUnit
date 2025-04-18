namespace ArticleForDT.Models
{
    public class ArticleResponseDto
    {
        public string Title { get; set; }
        public string Status { get; set; }
        public string ReviewComment { get; set; }
        public int? Grade { get; set; }
        public DateTime UploadDate { get; set; }
    }
}
