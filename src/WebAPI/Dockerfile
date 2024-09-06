# docker build . --no-cache --progress plain -t nq.expenditure:v1.4 -f src/WebAPI/Dockerfile
# docker build . -t nq.expenditure:v1.4 -f src/WebAPI/Dockerfile
# docker run -it --rm -p 8000:8080 nq.expenditure:v1.4
# http://localhost:8000/api/weatherforecast

# docker tag nq.expenditure:v1.4 nhatquang/nq.expenditure:v1.4
# docker push nhatquang/nq.expenditure:v1.4
# docker pull nhatquang/nq.expenditure:v1.4
# docker run -d -p 5001:5001 -e ASPNETCORE_HTTP_PORTS=5001 nhatquang/nq.expenditure:v1.4

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/WebAPI/WebAPI.csproj", "src/WebAPI/"]
COPY ["src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj", "src/Cex/Cex.Infrastructure/"]
COPY ["src/Cex/Cex.Application/Cex.Application.csproj", "src/Cex/Cex.Application/"]
COPY ["src/Libs/Lib.Application/Lib.Application.csproj", "src/Libs/Lib.Application/"]
COPY ["src/Libs/Lib.ExternalServices/Lib.ExternalServices.csproj", "src/Libs/Lib.ExternalServices/"]
COPY ["src/Cex/Cex.Domain/Cex.Domain.csproj", "src/Cex/Cex.Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
RUN dotnet restore "./src/WebAPI/WebAPI.csproj"
COPY . .
WORKDIR "/src/src/WebAPI"
RUN dotnet build "./WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebAPI.dll"]