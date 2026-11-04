# EnterpriseUI DevOps, CI/CD & Deployment Guide

This guide provides enterprise DevOps and SRE teams with standard automation pipelines, containerization patterns, and NuGet artifact management for EnterpriseUI.

---

## 1. Automated Build & Test Pipeline (GitHub Actions)

A reference `.github/workflows/ci.yml` pipeline that compiles all 47 projects, executes unit tests, validates code formatting, and packs signed NuGet packages:

```yaml
name: EnterpriseUI Build & Verify Pipeline

on:
  push:
    branches: [ main, release/* ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
      - name: Checkout Codebase
        uses: actions/checkout@v4

      - name: Setup .NET 8 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore Dependencies
        run: dotnet restore EnterpriseUI.slnx

      - name: Build Solution in Release Mode
        run: dotnet build EnterpriseUI.slnx -c Release --no-restore

      - name: Execute xUnit Test Suite
        run: dotnet test tests/EnterpriseUI.UnitTests/EnterpriseUI.UnitTests.csproj -c Release --no-build --verbosity normal --collect:"XPlat Code Coverage"

      - name: Run High-Performance Benchmarks (Verification Run)
        run: dotnet run --project benchmarks/EnterpriseUI.Benchmarks/EnterpriseUI.Benchmarks.csproj -c Release --no-build

      - name: Package Signed NuGet Artifacts
        if: github.ref == 'refs/heads/main'
        run: |
          dotnet pack src/EnterpriseUI.Core/EnterpriseUI.Core.csproj -c Release -o ./artifacts
          dotnet pack src/EnterpriseUI.Grids/EnterpriseUI.Grids.csproj -c Release -o ./artifacts
          dotnet pack src/EnterpriseUI.Theming/EnterpriseUI.Theming.csproj -c Release -o ./artifacts
```

---

## 2. Docker & Container Deployment (Blazor & ASP.NET Core)

For containerized microservices and Blazor web applications utilizing EnterpriseUI:

```dockerfile
# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore EnterpriseUI.slnx
RUN dotnet publish platforms/EnterpriseUI.Blazor/EnterpriseUI.Blazor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EnterpriseUI.Blazor.dll"]
```

---

## 3. Versioning & Release Strategy

EnterpriseUI adheres strictly to **Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`):

- **Major Versions** (`v1.0.0` -> `v2.0.0`): Breaking architectural shifts, framework target transitions (e.g. aligning with subsequent .NET LTS releases).
- **Minor Versions** (`v1.1.0` -> `v1.2.0`): New unified controls, additional theme packs, new query operators, backwards-compatible API additions.
- **Patch Versions** (`v1.0.1` -> `v1.0.2`): Bug fixes, memory optimizations, WCAG accessibility adjustments, security patches.
