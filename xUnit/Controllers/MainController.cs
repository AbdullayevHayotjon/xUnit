using ArticleForDT;
using ArticleForDT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using xUnit.Models;

namespace xUnit.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MainController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IJwtService _jwtService;
        private readonly ILogger<MainController> _logger;
        private readonly string _uploadFolder;

        public MainController(
            AppDbContext db,
            IJwtService jwtService,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<MainController> logger)
        {
            _db = db;
            _jwtService = jwtService;
            _logger = logger;
            // Configure upload folder via appsettings.json
            var relativePath = config.GetValue<string>("UploadSettings:RelativePath") ?? "uploads";
            _uploadFolder = Path.Combine(env.WebRootPath, relativePath);
        }

        [HttpPost("register")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _db.Users.AnyAsync(u => u.Email == dto.Email, cancellationToken))
                return Conflict(new { message = "Email already exists" });

            var user = new User
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            await _db.Users.AddAsync(user, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Registration successful" });
        }

        [HttpPost("login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto, CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Email == dto.Email.Trim().ToLowerInvariant(), cancellationToken);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            var token = _jwtService.GenerateToken(user);
            return Ok(new { token });
        }

        [Authorize(Roles = Roles.Student)]
        [HttpGet("articles/my")]
        public async Task<ActionResult<IEnumerable<ArticleResponseDto>>> GetMyArticles(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var studentId))
                return Unauthorized();

            var articles = await _db.Articles
                .Where(a => a.StudentId == studentId)
                .AsNoTracking()
                .Select(a => new ArticleResponseDto
                {
                    Title = a.Title,
                    Status = a.Status,
                    ReviewComment = a.ReviewComment,
                    Grade = a.Grade,
                    UploadDate = a.UploadDate
                })
                .ToListAsync(cancellationToken);

            return Ok(articles);
        }

        [Authorize(Roles = Roles.Student)]
        [HttpPost("articles/upload")]
        public async Task<IActionResult> UploadArticle([FromForm] ArticleUploadDto dto, CancellationToken cancellationToken)
        {
            if (dto.File == null)
                return BadRequest(new { message = "File is required" });

            if (Path.GetExtension(dto.File.FileName).ToLowerInvariant() != ".pdf")
                return BadRequest(new { message = "Only PDF files are allowed" });

            Directory.CreateDirectory(_uploadFolder);
            var uniqueFileName = $"{Guid.NewGuid():N}{Path.GetExtension(dto.File.FileName)}";
            var savedPath = Path.Combine(_uploadFolder, uniqueFileName);

            try
            {
                await using var stream = new FileStream(savedPath, FileMode.Create);
                await dto.File.CopyToAsync(stream, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error saving file {FileName}", dto.File.FileName);
                return StatusCode(500, new { message = "Could not save the file" });
            }

            var studentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var article = new Article
            {
                Title = dto.Title.Trim(),
                FilePath = Path.Combine("uploads", uniqueFileName),
                Status = ArticleStatus.Submitted,
                StudentId = studentId,
                UploadDate = DateTime.UtcNow
            };

            await _db.Articles.AddAsync(article, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Article submitted for review" });
        }

        [Authorize(Roles = Roles.Teacher)]
        [HttpGet("articles")]
        public async Task<ActionResult<IEnumerable<ArticleDetailsResponseDto>>> GetAllArticles(CancellationToken cancellationToken)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}/uploads";

            var articles = await _db.Articles
                .Include(a => a.Student)
                .AsNoTracking()
                .Select(a => new ArticleDetailsResponseDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    FileUrl = baseUrl + Path.GetFileName(a.FilePath),
                    Status = a.Status,
                    Grade = a.Grade,
                    ReviewComment = a.ReviewComment,
                    UploadDate = a.UploadDate,
                    Student = new StudentResponseDto
                    {
                        Id = a.Student.Id,
                        FirstName = a.Student.FirstName,
                        LastName = a.Student.LastName,
                        Email = a.Student.Email
                    }
                })
                .ToListAsync(cancellationToken);

            return Ok(articles);
        }

        [Authorize(Roles = Roles.Teacher)]
        [HttpPost("articles/{id}/review")]
        public async Task<IActionResult> ReviewArticle(int id, [FromBody] ReviewDto dto, CancellationToken cancellationToken)
        {
            var article = await _db.Articles.FindAsync(new object[] { id }, cancellationToken);
            if (article == null)
                return NotFound(new { message = "Article not found" });

            article.ReviewComment = dto.Comment?.Trim();
            article.Grade = dto.Grade;
            article.Status = ArticleStatus.Reviewed;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Article reviewed successfully" });
        }
    }

    // Supporting constants/enums
    public static class Roles
    {
        public const string Student = "Student";
        public const string Teacher = "Teacher";
    }

    public static class ArticleStatus
    {
        public const string Submitted = "Submitted";
        public const string Reviewed = "Reviewed";
    }
}
