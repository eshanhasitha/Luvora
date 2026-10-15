FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PromotionService/PromotionService.csproj PromotionService/
RUN dotnet restore PromotionService/PromotionService.csproj
COPY . .
WORKDIR /src/PromotionService
RUN dotnet publish PromotionService.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PromotionService.dll"]
