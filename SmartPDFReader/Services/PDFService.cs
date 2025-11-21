using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.AspNetCore.Http;

namespace SmartPDFReader.Services
{
    public class PDFService : IPDFService
    {
        private readonly ILogger<PDFService> _logger;

        public PDFService(ILogger<PDFService> logger = null)
        {
            _logger = logger;
        }

        public Dictionary<int, string> ExtractTextByPages(IFormFile pdfFile)
        {
            var pagesText = new Dictionary<int, string>();

            if (pdfFile == null || pdfFile.Length == 0)
            {
                pagesText[1] = "No PDF file provided.";
                return pagesText;
            }

            try
            {
                _logger?.LogInformation($"Starting PDF processing. File: {pdfFile.FileName}, Size: {pdfFile.Length} bytes");

                // Check file signature to verify it's a PDF
                using var stream = new MemoryStream();
                pdfFile.CopyTo(stream);
                stream.Position = 0;

                // Read first 5 bytes to check PDF signature
                byte[] header = new byte[5];
                stream.Read(header, 0, 5);
                stream.Position = 0;

                // Check if it's a valid PDF (starts with "%PDF-")
                if (!IsValidPdfHeader(header))
                {
                    _logger?.LogWarning("Invalid PDF header detected");
                    pagesText[1] = "[ERROR: Not a valid PDF file. File may be corrupted or not a PDF.]";
                    return pagesText;
                }

                _logger?.LogInformation("PDF header validation passed");

                using (var pdfReader = new PdfReader(stream))
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

                                _logger?.LogInformation($"Page {pageNum} - Raw text extracted. Length: {pageText?.Length ?? 0}");

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

                        // If we got garbage text or no real text, provide clear error
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
                _logger?.LogError(ex, "Error processing PDF");
                pagesText[1] = $"[ERROR: Failed to process PDF: {ex.Message}]";
            }

            return pagesText;
        }

        private bool IsValidPdfHeader(byte[] header)
        {
            // PDF files should start with "%PDF-"
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
                _logger?.LogDebug("Text is null or empty - classified as garbage");
                return true;
            }

            // Check for the corrupted character pattern
            if (text.Contains("������") || text.Contains("��"))
            {
                _logger?.LogDebug("Text contains garbage character patterns");
                return true;
            }

            // Check if text is mostly non-printable characters
            int printableCount = text.Count(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c));
            double printableRatio = (double)printableCount / text.Length;

            _logger?.LogDebug($"Text analysis - Printable characters: {printableCount}/{text.Length} ({printableRatio:P2})");

            bool isGarbage = printableRatio < 0.3; // Lowered threshold from 0.5 to 0.3
            if (isGarbage)
            {
                _logger?.LogDebug("Text classified as garbage due to low printable character ratio");
            }

            return isGarbage;
        }

        private bool IsUsefulText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[") || IsGarbageText(text))
            {
                _logger?.LogDebug("Text is not useful - null, empty, starts with bracket, or garbage");
                return false;
            }

            // Real text should have a reasonable number of spaces and letters
            var wordCount = GetWordCount(text);
            bool isUseful = wordCount > 2 && text.Length > 15; // Lowered thresholds

            _logger?.LogDebug($"Usefulness check - Words: {wordCount}, Length: {text.Length}, IsUseful: {isUseful}");

            return isUseful;
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

            // More aggressive cleaning
            string cleaned = text
                .Replace("-\n", "")     // Remove hyphenation
                .Replace("\n", " ")     // Replace newlines with spaces
                .Replace("\r", " ")     // Replace carriage returns with spaces
                .Replace("\t", " ")     // Replace tabs with spaces
                .Replace("  ", " ")     // Replace double spaces with single
                .Replace("  ", " ")     // Do it again to catch any remaining
                .Trim();

            _logger?.LogDebug($"Text cleaning - Before: {text.Length}, After: {cleaned.Length}");

            return cleaned;
        }
    }
}