FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AuditService/AuditService.csproj AuditService/
RUN dotnet restore AuditService/AuditService.csproj
COPY . .
WORKDIR /src/AuditService
RUN dotnet publish AuditService.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AuditService.dll"]
