
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/parse", async (HttpContext ctx) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var edi = await sr.ReadToEndAsync();
    var remit = X12EdiNetParser.ParseFromString(edi);   // instead of FlatFile835Parser
    await ctx.Response.WriteAsJsonAsync(remit);
});


app.Run("http://0.0.0.0:5000");
