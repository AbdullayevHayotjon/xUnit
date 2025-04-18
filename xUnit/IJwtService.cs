namespace ArticleForDT
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
