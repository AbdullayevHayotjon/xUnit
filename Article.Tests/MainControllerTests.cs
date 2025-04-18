using ArticleForDT;
using ArticleForDT.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using System.Text;
using xUnit.Controllers;
using xUnit.Models;

namespace xUnit.Tests
{
    public class MainControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly MainController _controller;

        public MainControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _jwtServiceMock = new Mock<IJwtService>();
            _controller = new MainController(_context, _jwtServiceMock.Object);
        }

        [Fact]
        public async Task Register_ShouldReturnOk_WhenNewUser()
        {
            var dto = new UserRegisterDto
            {
                FirstName = "Ali",
                LastName = "Valiyev",
                Email = "ali@mail.com",
                Password = "123456"
            };

            var result = await _controller.Register(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("User muvaffaqqiyatli ro'yhatdan o'tdi", ok.Value);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailExists()
        {
            _context.Users.Add(new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "exist@mail.com",
                PasswordHash = "hash"
            });
            await _context.SaveChangesAsync();

            var dto = new UserRegisterDto
            {
                FirstName = "Ali",
                LastName = "Valiyev",
                Email = "exist@mail.com",
                Password = "123456"
            };

            var result = await _controller.Register(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Email oldindan mavjud", bad.Value);
        }

        [Fact]
        public async Task Login_ShouldReturnToken_WhenCredentialsCorrect()
        {
            var password = "mypassword";
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            _context.Users.Add(new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "user@mail.com",
                PasswordHash = hash
            });
            await _context.SaveChangesAsync();

            var dto = new UserLoginDto { Email = "user@mail.com", Password = password };
            _jwtServiceMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("fake_token");

            var result = await _controller.Login(dto);
            var ok = Assert.IsType<OkObjectResult>(result);
            var token = ok.Value?.GetType().GetProperty("token")?.GetValue(ok.Value);
            Assert.Equal("fake_token", token);
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenWrongPassword()
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("correct");
            _context.Users.Add(new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "mail@mail.com",
                PasswordHash = hash
            });
            await _context.SaveChangesAsync();

            var dto = new UserLoginDto { Email = "mail@mail.com", Password = "wrong" };
            var result = await _controller.Login(dto);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetMyArticles_ShouldReturnList_WhenStudentLoggedIn()
        {
            var student = new User
            {
                FirstName = "F",
                LastName = "L",
                Email = "s@mail.com",
                PasswordHash = "p",
                Role = "Student"
            };
            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            _context.Articles.Add(new Article
            {
                Title = "Test Article",
                StudentId = student.Id,
                Status = "Yuborilgan",
                UploadDate = DateTime.UtcNow,
                FilePath = "uploads/test.pdf"
            });
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
        new Claim(ClaimTypes.Role, "Student")
    };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            var result = await _controller.GetMyArticles();
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsType<List<ArticleResponseDto>>(ok.Value);
            Assert.Single(list);

            var firstArticle = list.First();
            Assert.Equal("Test Article", firstArticle.Title);
        }

        [Fact]
        public async Task UploadArticle_ShouldReturnBadRequest_WhenNotPdf()
        {
            var student = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "pdf@mail.com",
                PasswordHash = "hash",
                Role = "Student"
            };
            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
                new Claim(ClaimTypes.Role, "Student")
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Fake content"));
            var file = new FormFile(stream, 0, stream.Length, "file", "not.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var dto = new ArticleUploadDto { Title = "Invalid File", File = file };

            var result = await _controller.UploadArticle(dto);
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Faqat .pdf fayl yuklash lozim", bad.Value);
        }

        [Fact]
        public async Task UploadArticle_ShouldReturnOk_WhenValidPdf()
        {
            var student = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "valid@mail.com",
                PasswordHash = "hash",
                Role = "Student"
            };
            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
                new Claim(ClaimTypes.Role, "Student")
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("PDF Content"));
            var file = new FormFile(stream, 0, stream.Length, "file", "file.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            var dto = new ArticleUploadDto { Title = "PDF Article", File = file };
            var result = await _controller.UploadArticle(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Maqola tekshiruvchiga yuborildi", ok.Value);
        }

        [Fact]
        public async Task GetAllArticles_ShouldReturnArticles_WhenTeacher()
        {
            var student = new User
            {
                FirstName = "S",
                LastName = "T",
                Email = "student@a.com",
                PasswordHash = "pass",
                Role = "Student"
            };
            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            _context.Articles.Add(new Article
            {
                Title = "Art",
                StudentId = student.Id,
                FilePath = "uploads/a.pdf",
                Status = "Yuborilgan",
                UploadDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Role, "Teacher"),
        new Claim(ClaimTypes.NameIdentifier, "999")
    };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims))
            };
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("localhost");

            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await _controller.GetAllArticles();
            var ok = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsType<List<ArticleDetailsResponseDto>>(ok.Value);
            Assert.Single(list);

            var firstArticle = list.First();
            Assert.Equal("Art", firstArticle.Title);
            Assert.Equal("http://localhost/uploads/a.pdf", firstArticle.FileUrl);
        }

        [Fact]
        public async Task ReviewArticle_ShouldReturnOk_WhenArticleFound()
        {
            var student = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "student@a.com",
                PasswordHash = "pass",
                Role = "Student"
            };
            _context.Users.Add(student);
            await _context.SaveChangesAsync();

            var article = new Article
            {
                Title = "Review Me",
                Status = "Yuborilgan",
                UploadDate = DateTime.UtcNow,
                StudentId = student.Id,
                FilePath = "uploads/review.pdf" // FilePath qo'shildi
            };
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Teacher") };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            var dto = new ReviewDto { Comment = "Zo‘r", Grade = 100 };
            var result = await _controller.ReviewArticle(article.Id, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Maqola muvaffaqqiyatli baholandi", ok.Value);
        }

        [Fact]
        public async Task ReviewArticle_ShouldReturnNotFound_WhenArticleNotFound()
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Teacher") };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };

            var dto = new ReviewDto { Comment = "Missing", Grade = 50 };
            var result = await _controller.ReviewArticle(999, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}