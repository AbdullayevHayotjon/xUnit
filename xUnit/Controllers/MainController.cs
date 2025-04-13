using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using xUnit.Models;

namespace xUnit.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtService _jwtService;
        public MainController(AppDbContext db, JwtService jwtService)
        {
            _db = db;
            _jwtService = jwtService;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data");

            // Foydalanuvchi mavjudligini tekshirish
            var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (existingUser != null)
                return BadRequest("Email oldindan mavjud");

            // Yangi foydalanuvchi ma'lumotlarini yaratish
            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            // Foydalanuvchini Users jadvaliga qo‘shish
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok("User muvaffaqqiyatli ro'yhatdan o'tdi");
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized("Email yoki Password xato");

            // Tokenni yaratish
            var tokenString = _jwtService.GenerateToken(user);

            return Ok(new { token = tokenString }); // Front-end ga token yuborish
        }



        [Authorize(Roles = "Student")]
        [HttpGet("articles/my")]
        public async Task<IActionResult> GetMyArticles()
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var articles = await _db.Articles
                .Where(a => a.StudentId == int.Parse(userId))
                .Select(a => new { a.Title, a.Status, a.ReviewComment, a.Grade, a.UploadDate })
                .ToListAsync();

            return Ok(articles);
        }

        [Authorize(Roles = "Student")]
        [HttpPost("articles/upload")]
        public async Task<IActionResult> UploadArticle([FromForm] ArticleUploadDto dto)
        {
            if (dto.File == null || !dto.File.FileName.EndsWith(".pdf"))
                return BadRequest("Faqat .pdf fayl yuklash lozim");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{dto.File.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await dto.File.CopyToAsync(stream);

            var article = new Article
            {
                Title = dto.Title,
                FilePath = Path.Combine("uploads", uniqueFileName),
                Status = "Yuborilgan",
                StudentId = int.Parse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value),
                UploadDate = DateTime.UtcNow
            };

            _db.Articles.Add(article);
            await _db.SaveChangesAsync();

            return Ok("Maqola tekshiruvchiga yuborildi");
        }


        [Authorize(Roles = "Teacher")]
        [HttpGet("articles")]
        public async Task<IActionResult> GetAllArticles()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var articles = await _db.Articles
                .Include(a => a.Student)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    FileUrl = baseUrl + "/uploads/" + Path.GetFileName(a.FilePath),
                    a.Status,
                    a.Grade,
                    a.ReviewComment,
                    a.UploadDate,
                    Student = new
                    {
                        a.Student.Id,
                        a.Student.FirstName,
                        a.Student.LastName,
                        a.Student.Email
                    }
                })
                .ToListAsync();

            return Ok(articles);
        }


        [Authorize(Roles = "Teacher")]
        [HttpPost("articles/{id}/review")]
        public async Task<IActionResult> ReviewArticle(int id, [FromBody] ReviewDto dto)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound();

            article.ReviewComment = dto.Comment;
            article.Grade = dto.Grade;
            article.Status = "Baholangan";

            await _db.SaveChangesAsync();

            return Ok("Maqola muvaffaqqiyatli baholandi");
        }

    }
}
