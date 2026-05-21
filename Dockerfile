# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Stage 2: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project file first for layer caching
COPY ["NetForum/NetForum.csproj", "NetForum/"]
RUN dotnet restore "NetForum/NetForum.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/NetForum"
RUN dotnet build "NetForum.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NetForum.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Stage 4: Final Image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root user for security
USER 1654

ENTRYPOINT ["dotnet", "NetForum.dll"]
