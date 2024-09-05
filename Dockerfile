# https://github.com/dotnet/dotnet-docker/blob/main/samples/aspnetapp/README.md

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# RUN echo $(ls -1 /src)
COPY . .
RUN ls -l
RUN dotnet restore "src/WebAPI/WebAPI.csproj" 
RUN dotnet build "src/WebAPI/WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/WebAPI/WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebAPI.dll"]

# docker build . --no-cache --progress plain -t nq.expenditure:1.2 -f Dockerfile
# docker run -it --rm -p 8000:8080 nq.expenditure:1.2
# http://localhost:8000/api/weatherforecast
