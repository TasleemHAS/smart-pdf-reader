using System.Text;
using UglyToad.PdfPig;

namespace SmartPDFReader.Services
{
    public class PDFTextExtractorService : IPDFTextExtractorService
    {
        private readonly IWebHostEnvironment _environment;

        public PDFTextExtractorService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> ExtractAllTextAsync(string filePath)
        {
            var fullPath = GetFullPath(filePath);
            return await Task.Run(() =>
            {
                try
                {
                    var text = new StringBuilder();
                    using (var document = PdfDocument.Open(fullPath))
                    {
                        foreach (var page in document.GetPages())
                        {
                            text.AppendLine(page.Text);
                            text.AppendLine(); // Add space between pages
                        }
                    }
                    return text.ToString().Trim();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error extracting text from PDF: {ex.Message}");
                }
            });
        }

        public async Task<List<string>> ExtractTextByPagesAsync(string filePath)
        {
            var fullPath = GetFullPath(filePath);
            return await Task.Run(() =>
            {
                var pages = new List<string>();
                using (var document = PdfDocument.Open(fullPath))
                {
                    foreach (var page in document.GetPages())
                    {
                        pages.Add(page.Text.Trim());
                    }
                }
                return pages;
            });
        }

        public async Task<string> SearchInPDFAsync(string filePath, string searchTerm)
        {
            var fullPath = GetFullPath(filePath);
            return await Task.Run(() =>
            {
                try
                {
                    var results = new StringBuilder();
                    using (var document = PdfDocument.Open(fullPath))
                    {
                        int pageNumber = 1;
                        foreach (var page in document.GetPages())
                        {
                            var pageText = page.Text;
                            if (pageText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                // Extract context around the search term
                                var context = ExtractContext(pageText, searchTerm);
                                results.AppendLine($"📄 **Page {pageNumber}:**");
                                results.AppendLine(context);
                                results.AppendLine("---");
                            }
                            pageNumber++;
                        }
                    }

                    var result = results.ToString().Trim();
                    return string.IsNullOrEmpty(result)
                        ? $"No direct matches found for '{searchTerm}' in the PDF. However, here's a general explanation based on the book content."
                        : result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error searching PDF: {ex.Message}");
                }
            });
        }

        private string ExtractContext(string pageText, string searchTerm)
        {
            var index = pageText.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (index == -1) return "Context not available.";

            // Get 200 characters before and after the search term
            var start = Math.Max(0, index - 200);
            var end = Math.Min(pageText.Length, index + searchTerm.Length + 200);
            var context = pageText.Substring(start, end - start);

            // Clean up the context
            context = context.Replace("\n", " ").Replace("  ", " ");
            return context.Trim() + "...";
        }

        private string GetFullPath(string filePath)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs");
            return Path.Combine(uploadsPath, filePath);
        }
    }
}