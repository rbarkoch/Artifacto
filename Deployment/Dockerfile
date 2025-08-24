# Multi-stage build for both API and WebApplication with NGINX


# ===== STAGE 1 - BUILD ===== #

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy all source code
COPY Source ./

# Restore dependencies
RUN dotnet restore

# Build and publish API
RUN dotnet publish "Artifacto.WebApi/Artifacto.WebApi.csproj" -c Release -o /app/webapi --no-restore

# Build and publish WebApplication
RUN dotnet publish "Artifacto.WebApplication/Artifacto.WebApplication.csproj" -c Release -o /app/webapp --no-restore

# =========================== #




# ===== STAGE 2 - DEPLOY ===== #

# Final stage with NGINX and .NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

ENV DOTNET_ENVIRONMENT=Production

# Install NGINX

RUN apt-get update && \
    apt-get install -y nginx supervisor openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create necessary directories

WORKDIR /app
RUN mkdir -p /app/webapi /app/webapp /data/storage /var/log/artifacto-webapi /var/log/artifacto-webapp /var/log/supervisor

# Copy published applications
COPY --from=build /app/webapi ./webapi/
COPY --from=build /app/webapp ./webapp/

# Copy configuration files
COPY Deployment/nginx.conf /etc/nginx/sites-available/default
COPY Deployment/supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Copy runtime entrypoint that will create/convert/accept user certificates
COPY Deployment/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Set proper permissions
RUN chown -R www-data:www-data /var/log/nginx && \
    chmod -R 755 /var/log/nginx && \
    mkdir -p /var/cache/nginx && \
    chown -R www-data:www-data /var/cache/nginx

# Expose ports
EXPOSE 80 443

# Start supervisor to manage all processes
CMD ["/app/entrypoint.sh"]

# =========================== #