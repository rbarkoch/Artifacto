using Artifacto.Database;
using Artifacto.FileStorage;
using Artifacto.Repository;
using Artifacto.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;

using Serilog;

/// <summary>
/// Entry point for the Artifacto Web API application.
/// Configures services, middleware, and starts the web server.
/// </summary>
internal class Program
{
    /// <summary>
    /// Application startup method. Configures logging, dependency injection, Kestrel server options,
    /// form upload limits, health checks, and controller endpoints.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    private static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext());
                
        // Add services to the container.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddArtifactoControllers();
        builder.Services.AddArtifactoSqliteDbContext();
        builder.Services.AddArtifactoFileStorage("/data/storage");
        builder.Services.AddArtifactoRepositories();
        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHealthChecks();


        // Configure form options to allow large file uploads with optimized settings
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue; // Remove multipart body size limit
            options.ValueLengthLimit = int.MaxValue; // Remove form value length limit
            options.KeyLengthLimit = int.MaxValue; // Remove form key length limit
            options.MultipartHeadersLengthLimit = int.MaxValue; // Remove multipart headers length limit
            options.MemoryBufferThreshold = 1024 * 1024; // 1MB threshold before buffering to disk
            options.BufferBody = false; // Disable buffering for better streaming performance
            options.MultipartBoundaryLengthLimit = int.MaxValue;
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

        // // Behind NGINX: respect X-Forwarded-* so Kestrel sees the original scheme/host
        // ForwardedHeadersOptions forwardedHeadersOptions = new()
        // {
        //     ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        // };

        // // NGINX runs in the same container and proxies from 127.0.0.1
        // forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
        // app.UseForwardedHeaders(forwardedHeadersOptions);

        app.UseHttpsRedirection();
        app.MapControllers();
        app.CreateOrMigrateDatabase();
        app.MapHealthChecks("/health");

        app.Run();
    }
}
