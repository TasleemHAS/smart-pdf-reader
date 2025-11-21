namespace SmartPDFReader.Services
{
    public interface ISimpleTextService
    {
        Task<string> GenerateAnswerAsync(string question, string bookTitle, string filePath);
        Task<bool> CanProcessPDF(string filePath);
    }
}