using Microsoft.AspNetCore.Mvc;
using Parkings.Web;
using Parkings.Web.Components;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(o =>
    {
        o.ServerCertificate = new X509Certificate2(
            "C:\\tmp\\cert\\certificate.pfx",
            "SmartParkings");
    });
});

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapGet("/api/camera/snapshot", async (HttpContext context,
    [FromQuery] string url,
    [FromQuery] string? type) =>
{
    try
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

        var isMjpeg = type?.ToUpper() == "MJPEG"
            || url.Contains("mjpg")
            || url.Contains("mjpeg");

        if (isMjpeg)
        {
            // Для MJPEG — читаем поток и извлекаем первый JPEG кадр
            using var resp = await client.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead);
            await using var stream = await resp.Content.ReadAsStreamAsync();

            var frameBytes = await ExtractFirstMjpegFrame(stream);
            if (frameBytes != null)
            {
                context.Response.ContentType = "image/jpeg";
                await context.Response.Body.WriteAsync(frameBytes);
            }
            else
            {
                context.Response.StatusCode = 502;
            }
        }
        else
        {
            // JPG — как раньше
            var bytes = await client.GetByteArrayAsync(url);
            context.Response.ContentType = "image/jpeg";
            await context.Response.Body.WriteAsync(bytes);
        }
    }
    catch
    {
        context.Response.StatusCode = 502;
    }
})
.AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
app.Run();

static async Task<byte[]?> ExtractFirstMjpegFrame(Stream stream)
{
    // MJPEG boundary: --boundary\r\nContent-Type: image/jpeg\r\n\r\n<JPEG bytes>\r\n
    // JPEG начинается с FF D8 и заканчивается FF D9
    var buffer = new byte[65536];
    var accumulated = new List<byte>();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, cts.Token);
            if (read == 0) break;

            accumulated.AddRange(buffer[..read]);

            var data = accumulated.ToArray();

            // Ищем начало JPEG (FF D8)
            int start = -1;
            for (int i = 0; i < data.Length - 1; i++)
            {
                if (data[i] == 0xFF && data[i + 1] == 0xD8)
                { start = i; break; }
            }

            if (start == -1) continue;

            // Ищем конец JPEG (FF D9) начиная с start
            int end = -1;
            for (int i = start; i < data.Length - 1; i++)
            {
                if (data[i] == 0xFF && data[i + 1] == 0xD9)
                { end = i + 1; break; }
            }

            if (end == -1) continue;

            // Нашли полный кадр
            return data[start..(end + 1)];
        }
    }
    catch (OperationCanceledException) { }

    return null;
}