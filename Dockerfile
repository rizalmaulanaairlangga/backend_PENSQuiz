# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files to restore dependencies
COPY *.sln ./
COPY src/PensQuiz.Api/PensQuiz.Api.csproj ./src/PensQuiz.Api/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY src/ ./src/

# Build and publish in Release mode
RUN dotnet publish src/PensQuiz.Api/PensQuiz.Api.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render exposes PORT env var, ASP.NET Core 8 listens on 8080 by default
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PensQuiz.Api.dll"]
