# Production Dockerfile for PhotoSite.Api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["PhotoSite.Api/PhotoSite.Api.csproj", "PhotoSite.Api/"]
RUN dotnet restore "PhotoSite.Api/PhotoSite.Api.csproj"

COPY . .
WORKDIR "/src/PhotoSite.Api"
RUN dotnet publish "PhotoSite.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhotoSite.Api.dll"]