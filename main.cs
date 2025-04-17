
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async (HttpContext context) => {
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(@"
<!DOCTYPE html>
<html>
<head>
    <title>Welcome</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 40px;
            line-height: 1.6;
            color: #333;
        }
        .container {
            max-width: 800px;
            margin: 0 auto;
            text-align: center;
        }
        h1 {
            color: #2c3e50;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Welcome to my ASP.NET Web App!</h1>
        <p>This is a simple HTML page being served from ASP.NET Core.</p>
    </div>
</body>
</html>");
});

app.Run("http://0.0.0.0:5000");
