namespace SmartPDFReader.Services
{
    public interface IPDFTextExtractorService
    {
        Task<string> ExtractAllTextAsync(string filePath);
        Task<List<string>> ExtractTextByPagesAsync(string filePath);
        Task<string> SearchInPDFAsync(string filePath, string searchTerm);
    }
}