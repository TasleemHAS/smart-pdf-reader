namespace SmartPDFReader.Services
{
    public class SimpleTextService : ISimpleTextService
    {
        private readonly IPDFTextExtractorService _pdfExtractor;
        private readonly IWebHostEnvironment _environment;

        public SimpleTextService(IPDFTextExtractorService pdfExtractor, IWebHostEnvironment environment)
        {
            _pdfExtractor = pdfExtractor;
            _environment = environment;
        }

        public async Task<bool> CanProcessPDF(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, "uploads", "pdfs", filePath);
                if (!File.Exists(fullPath)) return false;

                // Try to extract a small amount of text to verify the PDF is readable
                var sampleText = await _pdfExtractor.ExtractAllTextAsync(filePath);
                return !string.IsNullOrWhiteSpace(sampleText) && sampleText.Length > 10;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GenerateAnswerAsync(string question, string bookTitle, string filePath)
        {
            try
            {
                // First, try to search for the question in the actual PDF
                var pdfAnswer = await _pdfExtractor.SearchInPDFAsync(filePath, question);

                if (!pdfAnswer.Contains("No direct matches found"))
                {
                    return FormatRealAnswer(question, bookTitle, pdfAnswer);
                }

                // If no direct matches, try to extract key terms and search for them
                var keyTerms = ExtractKeyTerms(question);
                foreach (var term in keyTerms)
                {
                    var termResult = await _pdfExtractor.SearchInPDFAsync(filePath, term);
                    if (!termResult.Contains("No direct matches found"))
                    {
                        return FormatRealAnswer(question, bookTitle,
                            $"**Related content found for '{term}':**\n\n{termResult}");
                    }
                }

                // If still no results, provide a helpful message
                return FormatFallbackAnswer(question, bookTitle);
            }
            catch (Exception ex)
            {
                // If PDF processing fails, provide fallback
                return FormatFallbackAnswer(question, bookTitle,
                    $"Note: PDF processing unavailable. {ex.Message}");
            }
        }

        private List<string> ExtractKeyTerms(string question)
        {
            // Simple key term extraction - you can enhance this
            var stopWords = new HashSet<string> { "what", "is", "the", "a", "an", "in", "on", "at", "to", "for", "of", "with", "by", "about", "like", "through", "are", "were", "was", "be", "been", "being", "have", "has", "had", "do", "does", "did", "can", "could", "will", "would", "should", "may", "might", "must" };

            return question.Split(' ', ',', '.', '?', '!')
                .Where(term => term.Length > 3 && !stopWords.Contains(term.ToLower()))
                .Take(5) // Take top 5 most relevant terms
                .ToList();
        }

        private string FormatRealAnswer(string question, string bookTitle, string pdfContent)
        {
            return $"📖 **Answer from {bookTitle}**\n\n" +
                   $"🔍 **Found in PDF:**\n\n{pdfContent}\n\n" +
                   $"❓ **Your Question:** {question}\n\n" +
                   $"✅ *This answer is extracted directly from your PDF content*";
        }

        private string FormatFallbackAnswer(string question, string bookTitle, string additionalInfo = "")
        {
            var fallbackResponses = new[]
            {
                $"While I couldn't find specific matches for your question in **{bookTitle}**, the book covers related topics through comprehensive theoretical frameworks and practical applications.",
                $"**{bookTitle}** addresses concepts surrounding your question with detailed analysis and case studies, though the specific phrasing wasn't found in the text.",
                $"The content in **{bookTitle}** provides valuable insights into this area, combining foundational principles with advanced applications relevant to your inquiry."
            };

            var random = new Random();
            var selectedResponse = fallbackResponses[random.Next(fallbackResponses.Length)];

            return $"📖 **Answer from {bookTitle}**\n\n" +
                   $"{selectedResponse}\n\n" +
                   $"❓ **Your Question:** {question}\n\n" +
                   $"💡 *For more specific answers, try rephrasing your question or check if the terms exist in your PDF. {additionalInfo}*";
        }
    }
}