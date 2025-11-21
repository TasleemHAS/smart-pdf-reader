using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPDFReader.Data;
using SmartPDFReader.Models;
using SmartPDFReader.Services;
using Microsoft.AspNetCore.Hosting;

namespace SmartPDFReader.Controllers
{
    public class DebugController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPDFService _pdfService;
        private readonly IWebHostEnvironment _environment;

        public DebugController(AppDbContext context, IPDFService pdfService, IWebHostEnvironment environment)
        {
            _context = context;
            _pdfService = pdfService;
            _environment = environment;
        }

        public async Task<IActionResult> TestPDF(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Content("Book not found");
            }

            var pdfPath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs", book.FilePath);

            if (!System.IO.File.Exists(pdfPath))
            {
                return Content("PDF file not found");
            }

            // Read the file and create FormFile
            byte[] fileBytes;
            using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
            {
                fileBytes = new byte[fileStream.Length];
                await fileStream.ReadAsync(fileBytes, 0, (int)fileStream.Length);
            }

            using var memoryStream = new MemoryStream(fileBytes);
            var formFile = new FormFile(memoryStream, 0, fileBytes.Length, "book", book.FilePath)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            // Extract text from PDF
            var pagesText = _pdfService.ExtractTextByPages(formFile);

            var result = $"<h1>PDF Debug Info for: {book.Title}</h1>";
            result += $"<h3>File: {book.FilePath}</h3>";
            result += $"<h3>Total Pages: {pagesText.Count}</h3>";
            result += $"<h3>File Size: {new FileInfo(pdfPath).Length} bytes</h3>";

            foreach (var page in pagesText)
            {
                result += $"<h4>Page {page.Key} ({page.Value?.Length ?? 0} characters):</h4>";

                if (page.Value?.StartsWith("[") == true || string.IsNullOrWhiteSpace(page.Value))
                {
                    result += $"<div style='color: red;'><strong>{page.Value}</strong></div>";
                }
                else
                {
                    // Show first 500 characters of each page
                    var preview = page.Value.Length > 500 ? page.Value.Substring(0, 500) + "..." : page.Value;
                    result += $"<textarea style='width:100%; height:150px; font-family: monospace;'>{preview}</textarea>";

                    // Show word count
                    var wordCount = page.Value.Split(new char[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    result += $"<p><strong>Word Count:</strong> {wordCount} words</p>";
                }
                result += "<hr>";
            }

            return Content(result, "text/html");
        }

        public async Task<IActionResult> SearchTest(int bookId, string searchTerm)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Content("Book not found");
            }

            var pdfPath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs", book.FilePath);

            if (!System.IO.File.Exists(pdfPath))
            {
                return Content("PDF file not found");
            }

            // Read the file and create FormFile
            byte[] fileBytes;
            using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
            {
                fileBytes = new byte[fileStream.Length];
                await fileStream.ReadAsync(fileBytes, 0, (int)fileStream.Length);
            }

            using var memoryStream = new MemoryStream(fileBytes);
            var formFile = new FormFile(memoryStream, 0, fileBytes.Length, "book", book.FilePath)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            // Extract text from PDF
            var pagesText = _pdfService.ExtractTextByPages(formFile);

            var result = $"<h1>Search Test for: '{searchTerm}'</h1>";
            result += $"<h3>Book: {book.Title}</h3>";

            var foundMatches = false;

            foreach (var page in pagesText)
            {
                if (!string.IsNullOrWhiteSpace(page.Value) && page.Value.ToLower().Contains(searchTerm.ToLower()))
                {
                    foundMatches = true;
                    result += $"<h4 style='color: green;'>✓ Found on Page {page.Key}</h4>";

                    // Show context around the search term
                    var pageText = page.Value;
                    var index = pageText.ToLower().IndexOf(searchTerm.ToLower());
                    var start = Math.Max(0, index - 100);
                    var length = Math.Min(300, pageText.Length - start);
                    var context = pageText.Substring(start, length);

                    result += $"<div style='background: #f0f8ff; padding: 10px; border-radius: 5px;'>";
                    result += $"<strong>Context:</strong> ...{context}...";
                    result += "</div>";
                    result += "<hr>";
                }
            }

            if (!foundMatches)
            {
                result += $"<h4 style='color: red;'>✗ No matches found for '{searchTerm}'</h4>";
            }

            return Content(result, "text/html");
        }
    }
}