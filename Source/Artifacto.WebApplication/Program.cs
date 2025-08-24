using System.Net.Http;
using System.Threading;
using System;

using Artifacto.Client;
using Artifacto.WebApplication.Components;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MudBlazor.Services;

using Serilog;

/// <summary>
/// Application entry point for the Artifacto WebApplication.
/// Configures services, middleware and runs the Blazor server application.
/// </summary>
internal class Program
{
    /// <summary>
    /// Main entry method. Builds and runs the web host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    private static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

         builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext());

        builder.Services.AddMudServices();
        builder.Services.AddHttpClient();
        builder.Services.AddControllers(); // Add controller support
        
        // Configure form options for large file uploads with optimized settings
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue; // Remove multipart body length limit
            options.ValueLengthLimit = int.MaxValue; // Remove value length limit
            options.ValueCountLimit = int.MaxValue; // Remove value count limit
            options.KeyLengthLimit = int.MaxValue; // Remove key length limit
            options.MemoryBufferThreshold = 1024 * 1024; // 1MB threshold before buffering to disk
            options.BufferBody = false; // Disable buffering for better streaming performance
            options.MultipartBoundaryLengthLimit = int.MaxValue;
        });
        
        builder.Services.AddRazorComponents()
                        .AddInteractiveServerComponents();
        builder.Services.AddSingleton<ArtifactoClient>(serviceProvider =>
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            string baseUrl = configuration["ArtifactoApi:BaseUrl"] ?? "https://localhost:7001";
            HttpClient httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan; // Set no timeout for HTTP requests
            return new ArtifactoClient(baseUrl, httpClient);
        });

        builder.WebHost.ConfigureKestrel(options => 
        {
            options.Limits.MaxRequestBodySize = long.MaxValue;
            options.Limits.MinRequestBodyDataRate = null; // Disable minimum data rate for large uploads
            options.Limits.MinResponseDataRate = null; // Disable minimum data rate for large downloads
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
        });

        WebApplication app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        // app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapControllers(); // Add controller routing
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
           .AddInteractiveServerRenderMode();


        app.Run();
    }
}
