FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY global.json ./
COPY src/Infosphere.Core/Infosphere.Core.csproj src/Infosphere.Core/
COPY src/Infosphere.Api/Infosphere.Api.csproj src/Infosphere.Api/

RUN dotnet restore src/Infosphere.Api/Infosphere.Api.csproj

COPY src/Infosphere.Core/ src/Infosphere.Core/
COPY src/Infosphere.Api/ src/Infosphere.Api/

RUN dotnet publish src/Infosphere.Api/Infosphere.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Infosphere.Api.dll"]
