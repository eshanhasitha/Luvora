FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY CartService/CartService.csproj CartService/
RUN dotnet restore CartService/CartService.csproj

COPY . .

WORKDIR /src/CartService

RUN dotnet publish CartService.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false



FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "CartService.dll"]