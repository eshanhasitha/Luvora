FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ReviewService/ReviewService.csproj ReviewService/
RUN dotnet restore ReviewService/ReviewService.csproj

COPY . .

WORKDIR /src/ReviewService

RUN dotnet publish ReviewService.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ReviewService.dll"]