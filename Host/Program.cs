using System.Text.Json;
using Host.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

string modelDirectory = Environment.GetEnvironmentVariable("RENZYU_AI_MODEL_DIRECTORY");
if (string.IsNullOrWhiteSpace(modelDirectory))
    modelDirectory = Path.Combine(builder.Environment.ContentRootPath, "TrainedModels");
Directory.CreateDirectory(modelDirectory);
builder.Services.AddSingleton<IAiModelCatalog>(services =>
    new FileAiModelCatalog(
        modelDirectory,
        services.GetRequiredService<ILogger<FileAiModelCatalog>>()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<GameHub>("/gameHub");

app.Run();
