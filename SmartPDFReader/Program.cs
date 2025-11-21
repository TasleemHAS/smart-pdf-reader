using Microsoft.EntityFrameworkCore;
using SmartPDFReader.Data;
using SmartPDFReader.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register PDF services
builder.Services.AddScoped<IPDFTextExtractorService, PDFTextExtractorService>();
builder.Services.AddScoped<ISimpleTextService, SimpleTextService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Books}/{action=Index}/{id?}");

// Create upload directory if it doesn't exist
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads", "pdfs");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.Run();