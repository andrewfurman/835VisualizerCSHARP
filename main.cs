// FILE: main.cs
// ----------------------------------------
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System; // <<< Add for Exception
using System.IO;
using System.Text;
using System.Text.Json; // <<< Add for JsonException

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/parse", async (HttpContext ctx) =>
{
    Console.WriteLine($"[{DateTime.UtcNow:u}] Received request for /api/parse"); // <<< DEBUGGING
    string edi = ""; // Initialize outside try block
    try
    {
        using var sr = new StreamReader(ctx.Request.Body);
        edi = await sr.ReadToEndAsync();
        Console.WriteLine($"[{DateTime.UtcNow:u}] Received EDI (first 500 chars): {edi.Substring(0, Math.Min(edi.Length, 500))}"); // <<< DEBUGGING

        // Wrap the parser call in a try-catch
        var remit = X12EdiNetParser.ParseFromString(edi); // Call the parser
        Console.WriteLine($"[{DateTime.UtcNow:u}] Successfully parsed EDI. Payer: {remit.PayerName}, Claims: {remit.Claims.Count}"); // <<< DEBUGGING

        await ctx.Response.WriteAsJsonAsync(remit);
        Console.WriteLine($"[{DateTime.UtcNow:u}] Sent successful JSON response."); // <<< DEBUGGING
    }
    catch (JsonException jsonEx) // Catch potential issues writing the JSON response
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR: Failed to serialize response to JSON. {jsonEx.Message}"); // <<< DEBUGGING
        Console.WriteLine($"[{DateTime.UtcNow:u}] Exception Details: {jsonEx}"); // <<< DEBUGGING
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsync($"Internal Server Error: Failed to serialize response. {jsonEx.Message}");
    }
    catch (Exception ex) // Catch general exceptions during request handling or parsing
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR: An error occurred processing the request. {ex.Message}"); // <<< DEBUGGING
        Console.WriteLine($"[{DateTime.UtcNow:u}] Exception Details: {ex}"); // <<< DEBUGGING (Full exception details)
        Console.WriteLine($"[{DateTime.UtcNow:u}] Raw EDI causing error (first 500 chars): {edi.Substring(0, Math.Min(edi.Length, 500))}"); // <<< DEBUGGING (Log EDI that caused error)

        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError; // Use 500 for server-side errors
        // Send a more informative error message back to the client
        await ctx.Response.WriteAsJsonAsync(new { error = "Error parsing EDI data on the server.", details = ex.Message });
    }
});

app.Run("http://0.0.0.0:5000");