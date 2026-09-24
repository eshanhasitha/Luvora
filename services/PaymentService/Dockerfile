FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY PaymentService/PaymentService.csproj PaymentService/
RUN dotnet restore PaymentService/PaymentService.csproj

COPY . .

WORKDIR /src/PaymentService

RUN dotnet publish PaymentService.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PaymentService.dll"]