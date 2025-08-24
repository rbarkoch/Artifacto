
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src
COPY . .
RUN dotnet restore "Artifacto.WebApplication/Artifacto.WebApplication.csproj"
RUN dotnet publish "Artifacto.WebApplication/Artifacto.WebApplication.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Artifacto.WebApplication.dll"]

