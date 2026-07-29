# docker build . --no-cache --progress plain -t qex.api:v1.15 -f src/WebAPI/Dockerfile
# docker build . -t qex.api:1.15 -f src/WebAPI/Dockerfile
# docker run -it --rm -p 8000:8080 qex.api:1.15
# http://localhost:8000/api/weatherforecast

# docker tag qex.api:1.15 nhatquang/qex.api:1.15
# docker push nhatquang/qex.api:1.15
# docker pull nhatquang/qex.api:1.15
# docker run -d -p 5001:5001 -e ASPNETCORE_HTTP_PORTS=5001 nhatquang/qex.api:1.15

# Base stage: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl openssl && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /certs && chown app:app /certs
USER app
WORKDIR /app

EXPOSE 5000
EXPOSE 5001

# Build stage: SDK image to restore and build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# Skip online CRL/OCSP revocation checks — required when a package signing cert is revoked
ENV NUGET_CERT_REVOCATION_MODE=offline
WORKDIR /src

# Copy project files and restore as distinct layers
COPY ["src/WebAPI/WebAPI.csproj", "src/WebAPI/"]
COPY ["Directory.Packages.props", "./"]
COPY ["NuGet.Config", "./"]
COPY ["src/Aspire/ServiceDefaults/ServiceDefaults.csproj", "src/Aspire/ServiceDefaults/"]
COPY ["src/Auth/Auth.Application/Auth.Application.csproj", "src/Auth/Auth.Application/"]
COPY ["src/Auth/Auth.Domain/Auth.Domain.csproj", "src/Auth/Auth.Domain/"]
COPY ["src/Auth/Auth.Infrastructure/Auth.Infrastructure.csproj", "src/Auth/Auth.Infrastructure/"]

COPY ["src/Cex/Cex.Application/Cex.Application.csproj", "src/Cex/Cex.Application/"]
COPY ["src/Cex/Cex.Domain/Cex.Domain.csproj", "src/Cex/Cex.Domain/"]
COPY ["src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj", "src/Cex/Cex.Infrastructure/"]

COPY ["src/Libs/Lib.Application/Lib.Application.csproj", "src/Libs/Lib.Application/"]
COPY ["src/Libs/Lib.ExternalServices/Lib.ExternalServices.csproj", "src/Libs/Lib.ExternalServices/"]
COPY ["src/Libs/Lib.Notifications/Lib.Notifications.csproj", "src/Libs/Lib.Notifications/"]

RUN dotnet restore "./src/WebAPI/WebAPI.csproj"

# Copy the remaining source code to build and publish
COPY . .

# Publish stage: dotnet publish already compiles — no separate build step needed
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.1
WORKDIR "/src/src/WebAPI"
RUN dotnet publish "./WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Version=${VERSION}

FROM base AS final
ARG VERSION=1.1
WORKDIR /app
ENV Application__Version=${VERSION}
COPY --from=publish /app/publish .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet/DataProtection-Keys
COPY --chown=app:app src/WebAPI/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh
# Run the app
ENTRYPOINT ["/app/docker-entrypoint.sh"]