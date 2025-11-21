using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPDFReader.Data;
using SmartPDFReader.Models;
using SmartPDFReader.Services;

namespace SmartPDFReader.Controllers
{
    public class BooksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BooksController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var books = await _context.Books.ToListAsync();
            return View(books);
        }

        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(Book book, IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a PDF file.");
                return View(book);
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf" };
            var fileExtension = Path.GetExtension(pdfFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError("", "Please upload only PDF files.");
                return View(book);
            }

            // Validate file size (10MB max)
            if (pdfFile.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("", "File size must be less than 10MB.");
                return View(book);
            }

            try
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs");
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save the file first
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                // SIMPLE PDF VALIDATION - Just check if it's a PDF and has content
                var isValidPdf = await IsValidPdfSimple(filePath);
                if (!isValidPdf)
                {
                    // Delete the invalid file
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                    ModelState.AddModelError("", "The file is not a valid PDF or may be corrupted. Please upload a valid PDF file.");
                    return View(book);
                }

                // Save to database
                book.FilePath = fileName;
                book.UploadDate = DateTime.Now;
                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Book '{book.Title}' uploaded successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                return View(book);
            }
        }

        private async Task<bool> IsValidPdfSimple(string filePath)
        {
            try
            {
                // Check if file exists and has content
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                    return false;

                // Simple PDF signature check
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] header = new byte[5];
                await fileStream.ReadAsync(header, 0, 5);

                // Check for PDF signature: %PDF-
                bool isValidSignature = header[0] == 0x25 && // %
                                       header[1] == 0x50 && // P
                                       header[2] == 0x44 && // D
                                       header[3] == 0x46 && // F
                                       header[4] == 0x2D;   // -

                return isValidSignature;
            }
            catch
            {
                return false;
            }
        }

        // DELETE BOOK
        [HttpPost]
        public async Task<IActionResult> DeleteBook(int id)
        {
            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book != null)
                {
                    // Delete associated questions first
                    var questions = _context.Questions.Where(q => q.BookId == id);
                    _context.Questions.RemoveRange(questions);

                    // Delete the book
                    _context.Books.Remove(book);
                    await _context.SaveChangesAsync();

                    // Delete the PDF file
                    var filePath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs", book.FilePath);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }

                    TempData["SuccessMessage"] = $"Book '{book.Title}' deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Book not found!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting book: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}