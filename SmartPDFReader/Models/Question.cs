using System.ComponentModel.DataAnnotations;

namespace SmartPDFReader.Models
{
    public class Question
    {
        public int Id { get; set; }

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        public int PageNumber { get; set; }

        public DateTime AskedDate { get; set; } = DateTime.Now;

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;
    }
}