using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.AspNetCore.Http;

namespace SmartPDFReader.Services
{
    public interface IFilePDFService
    {
        Dictionary<int, string> ExtractTextByPages(string filePath);
    }

    public class FilePDFService : IFilePDFService
    {
        private readonly ILogger<FilePDFService> _logger;

        public FilePDFService(ILogger<FilePDFService> logger = null)
        {
            _logger = logger;
        }

        public Dictionary<int, string> ExtractTextByPages(string filePath)
        {
            var pagesText = new Dictionary<int, string>();

            try
            {
                _logger?.LogInformation($"Starting PDF processing from file path: {filePath}");

                if (!System.IO.File.Exists(filePath))
                {
                    pagesText[1] = "[ERROR: PDF file not found.]";
                    return pagesText;
                }

                var fileInfo = new FileInfo(filePath);
                _logger?.LogInformation($"File exists. Size: {fileInfo.Length} bytes");

                // Read file signature
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] header = new byte[5];
                fileStream.Read(header, 0, 5);
                fileStream.Position = 0;

                if (!IsValidPdfHeader(header))
                {
                    _logger?.LogWarning("Invalid PDF header detected");
                    pagesText[1] = "[ERROR: Not a valid PDF file. File may be corrupted or not a PDF.]";
                    return pagesText;
                }

                _logger?.LogInformation("PDF header validation passed");

                using (var pdfReader = new PdfReader(filePath))
                {
                    // Check if PDF is encrypted
                    if (pdfReader.IsEncrypted())
                    {
                        _logger?.LogWarning("PDF is encrypted/protected");
                        pagesText[1] = "[ERROR: PDF is encrypted/protected. Cannot extract text from protected files.]";
                        return pagesText;
                    }

                    using (var pdfDoc = new PdfDocument(pdfReader))
                    {
                        int totalPages = pdfDoc.GetNumberOfPages();
                        _logger?.LogInformation($"PDF has {totalPages} pages");

                        if (totalPages == 0)
                        {
                            pagesText[1] = "[ERROR: PDF has no pages.]";
                            return pagesText;
                        }

                        bool foundUsefulText = false;

                        for (int pageNum = 1; pageNum <= totalPages; pageNum++)
                        {
                            try
                            {
                                _logger?.LogInformation($"Processing page {pageNum}");

                                var strategy = new SimpleTextExtractionStrategy();
                                var pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(pageNum), strategy);

                                _logger?.LogInformation($"Page {pageNum} - Raw text length: {pageText?.Length ?? 0}");

                                if (!string.IsNullOrWhiteSpace(pageText))
                                {
                                    // Log a sample of the extracted text for debugging
                                    var sampleText = pageText.Length > 100 ? pageText.Substring(0, 100) + "..." : pageText;
                                    _logger?.LogInformation($"Page {pageNum} - Sample text: '{sampleText}'");

                                    if (!IsGarbageText(pageText))
                                    {
                                        // Clean the text
                                        pageText = CleanText(pageText);
                                        _logger?.LogInformation($"Page {pageNum} - Text after cleaning. Length: {pageText.Length}");

                                        if (IsUsefulText(pageText))
                                        {
                                            pagesText[pageNum] = pageText;
                                            foundUsefulText = true;
                                            _logger?.LogInformation($"✅ SUCCESS: Found useful text on page {pageNum}. Word count: {GetWordCount(pageText)}");

                                            // If we got good text, stop processing to save time
                                            if (pageText.Length > 100)
                                            {
                                                _logger?.LogInformation($"Stopping extraction after page {pageNum} - sufficient text found");
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            _logger?.LogWarning($"Page {pageNum} - Text not considered useful. Word count: {GetWordCount(pageText)}, Length: {pageText.Length}");
                                            pagesText[pageNum] = $"[Page {pageNum}: Text extracted but doesn't meet usefulness criteria]";
                                        }
                                    }
                                    else
                                    {
                                        _logger?.LogWarning($"Page {pageNum} - Text classified as garbage");
                                        pagesText[pageNum] = $"[Page {pageNum}: Extracted text appears to be corrupted or garbage]";
                                    }
                                }
                                else
                                {
                                    _logger?.LogWarning($"Page {pageNum} - No text extracted (null or empty)");
                                    pagesText[pageNum] = $"[Page {pageNum}: No readable text - may be images or corrupted]";
                                }
                            }
                            catch (Exception pageEx)
                            {
                                _logger?.LogError(pageEx, $"Error processing page {pageNum}");
                                pagesText[pageNum] = $"[Page {pageNum} Error: {pageEx.Message}]";
                            }
                        }

                        if (!foundUsefulText)
                        {
                            _logger?.LogWarning("No useful text found in any page of the PDF");
                            pagesText[1] = "[ERROR: PDF appears to be corrupted, encrypted, or contains only images. No readable text could be extracted.]";
                        }
                        else
                        {
                            _logger?.LogInformation("PDF processing completed successfully with useful text found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing PDF from file path");
                pagesText[1] = $"[ERROR: Failed to process PDF: {ex.Message}]";
            }

            return pagesText;
        }

        private bool IsValidPdfHeader(byte[] header)
        {
            return header.Length >= 5 &&
                   header[0] == 0x25 && // %
                   header[1] == 0x50 && // P
                   header[2] == 0x44 && // D
                   header[3] == 0x46 && // F
                   header[4] == 0x2D;   // -
        }

        private bool IsGarbageText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (text.Contains("������") || text.Contains("��"))
            {
                return true;
            }

            int printableCount = text.Count(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c));
            double printableRatio = (double)printableCount / text.Length;

            return printableRatio < 0.3;
        }

        private bool IsUsefulText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[") || IsGarbageText(text))
            {
                return false;
            }

            var wordCount = GetWordCount(text);
            return wordCount > 2 && text.Length > 15;
        }

        private int GetWordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return text
                .Replace("-\n", "")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }
}