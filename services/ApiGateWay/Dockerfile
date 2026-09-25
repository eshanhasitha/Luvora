FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ApiGateWay/ApiGateWay.csproj ApiGateWay/
RUN dotnet restore ApiGateWay/ApiGateWay.csproj

COPY . .

WORKDIR /src/ApiGateWay

RUN dotnet publish ApiGateWay.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false



FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ApiGateWay.dll"]