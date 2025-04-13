public class ArticleUploadDto
{
    public string Title { get; set; }  // Maqola nomi
    public string Content { get; set; }  // Maqola matni
    public IFormFile File { get; set; }  // Fayl (agar fayl yuborilsa)
}
