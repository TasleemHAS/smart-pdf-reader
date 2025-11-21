using Microsoft.EntityFrameworkCore;
using SmartPDFReader.Models;

namespace SmartPDFReader.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Book> Books { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Book)
                .WithMany()
                .HasForeignKey(q => q.BookId);
        }
    }
}