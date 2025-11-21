using Microsoft.AspNetCore.Http;

namespace SmartPDFReader.Services
{
    public interface IPDFService
    {
        Dictionary<int, string> ExtractTextByPages(IFormFile pdfFile);
    }
}