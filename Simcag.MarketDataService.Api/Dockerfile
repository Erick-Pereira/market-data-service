# Build context: raiz do repositório
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["shared/shared.csproj", "shared/"]
COPY ["market-data-service/Simcag.MarketDataService.Api/market-data-service.csproj", "market-data-service/Simcag.MarketDataService.Api/"]
COPY ["market-data-service/Simcag.MarketDataService.Application/Simcag.MarketDataService.Application.csproj", "market-data-service/Simcag.MarketDataService.Application/"]
COPY ["market-data-service/Simcag.MarketDataService.Domain/Simcag.MarketDataService.Domain.csproj", "market-data-service/Simcag.MarketDataService.Domain/"]
COPY ["market-data-service/Simcag.MarketDataService.Infrastructure/Simcag.MarketDataService.Infrastructure.csproj", "market-data-service/Simcag.MarketDataService.Infrastructure/"]
RUN dotnet restore "market-data-service/Simcag.MarketDataService.Api/market-data-service.csproj"
COPY shared/ shared/
COPY market-data-service/ market-data-service/
RUN dotnet publish "market-data-service/Simcag.MarketDataService.Api/market-data-service.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Simcag.MarketDataService.Api.dll"]
