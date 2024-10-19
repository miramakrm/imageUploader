using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.MapPost("/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Form content type is required.");

    var form = await request.ReadFormAsync();
    var title = form["title"].ToString();
    var file = form.Files["image"];

    // Validation 
    if (string.IsNullOrWhiteSpace(title) || file == null || file.Length == 0)
        return Results.BadRequest("Title and valid image file are required.");

    var allowedExtensions = new[] { "image/jpeg", "image/png", "image/gif" };
    if (!allowedExtensions.Contains(file.ContentType))
        return Results.BadRequest("Only jpeg, png, and gif files are allowed.");

    var fileId = Guid.NewGuid().ToString();
    var filePath = Path.Combine("wwwroot", "uploads", $"{fileId}_{file.FileName}");


    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }
    var imageData = new ImageData { Id = fileId, Title = title, FileName = filePath };
    var jsonData = Path.Combine("data", "images.json");

    var images = new List<ImageData>();
    if (File.Exists(jsonData))
    {
        var json = await File.ReadAllTextAsync(jsonData);
        images = JsonSerializer.Deserialize<List<ImageData>>(json) ?? new List<ImageData>();
    }


    images.Add(imageData);
    await File.WriteAllTextAsync(jsonData, JsonSerializer.Serialize(images, new JsonSerializerOptions { WriteIndented = true }));

    return Results.Redirect($"/picture/{fileId}");
});

app.MapGet("/picture/{id}", async (string id) =>
{
    var jsonData = Path.Combine("data", "images.json");
    if (!File.Exists(jsonData))
        return Results.NotFound("No images found.");

    var existingData = await File.ReadAllTextAsync(jsonData);
    var images = JsonSerializer.Deserialize<List<ImageData>>(existingData) ?? new List<ImageData>();

    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null)
        return Results.NotFound("Image not found.");

    var imagePath = image.FileName;
    var fileName = Path.GetFileName(imagePath);
    var imageUrl = $"/uploads/{fileName}";

    return Results.Text($@"
        <html>
        <body>
            <h1>{image.Title}</h1>
            <img src='{imageUrl}' alt='Uploaded Image' />
        </body>
        </html>", "text/html");
});

app.Run();
