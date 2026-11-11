# Production Dockerfile for PhotoSite.Web (Blazor Server)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["PhotoSite.Web/PhotoSite.Web.csproj", "PhotoSite.Web/"]
RUN dotnet restore "PhotoSite.Web/PhotoSite.Web.csproj"

COPY . .
WORKDIR "/src/PhotoSite.Web"
RUN dotnet publish "PhotoSite.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhotoSite.Web.dll"]
