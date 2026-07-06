FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/JobOrchestrator.Domain/JobOrchestrator.Domain.csproj src/JobOrchestrator.Domain/
COPY src/JobOrchestrator.Application/JobOrchestrator.Application.csproj src/JobOrchestrator.Application/
COPY src/JobOrchestrator.Infrastructure/JobOrchestrator.Infrastructure.csproj src/JobOrchestrator.Infrastructure/
COPY src/JobOrchestrator.Api/JobOrchestrator.Api.csproj src/JobOrchestrator.Api/
RUN dotnet restore src/JobOrchestrator.Api/JobOrchestrator.Api.csproj

COPY src/JobOrchestrator.Domain/ src/JobOrchestrator.Domain/
COPY src/JobOrchestrator.Application/ src/JobOrchestrator.Application/
COPY src/JobOrchestrator.Infrastructure/ src/JobOrchestrator.Infrastructure/
COPY src/JobOrchestrator.Api/ src/JobOrchestrator.Api/

RUN dotnet publish src/JobOrchestrator.Api/JobOrchestrator.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "JobOrchestrator.Api.dll"]
