using System.Security.Claims;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

        private const long MaxFileSize = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public FilesController(ApplicationDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        [HttpPost("upload")]
        [HttpPost("/api/upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "Không thể xác định người dùng hiện tại." });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file cần upload." });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { message = "File vượt quá giới hạn 5MB." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ file PDF, JPG, PNG." });
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                return BadRequest(new { message = "Định dạng nội dung file không hợp lệ." });
            }

            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads", userId.ToString());
            Directory.CreateDirectory(uploadsRoot);

            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            var normalizedSafeName = string.Concat(safeName.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
            if (string.IsNullOrWhiteSpace(normalizedSafeName))
            {
                normalizedSafeName = "document";
            }

            var uniqueFileName = $"{normalizedSafeName}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var absolutePath = Path.Combine(uploadsRoot, uniqueFileName);

            await using (var stream = new FileStream(absolutePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = Path.Combine("uploads", userId.ToString(), uniqueFileName).Replace('\\', '/');
            var fileRecord = new UserFileRecord
            {
                UserId = userId,
                FileName = uniqueFileName,
                FilePath = relativePath,
                FileType = file.ContentType,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.UserFiles.Add(fileRecord);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                id = fileRecord.Id,
                fileName = fileRecord.FileName,
                fileType = fileRecord.FileType,
                filePath = $"/{fileRecord.FilePath}",
                createdAt = fileRecord.CreatedAt
            });
        }

        [HttpGet("my")]
        public IActionResult GetMyFiles()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var files = _dbContext.UserFiles
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FileType,
                    filePath = $"/{f.FilePath}",
                    f.CreatedAt
                })
                .ToList();

            return Ok(files);
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            return int.TryParse(userIdStr, out userId);
        }
    }
}
