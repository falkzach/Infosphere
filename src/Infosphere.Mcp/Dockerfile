FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY global.json ./
COPY src/Infosphere.Mcp/Infosphere.Mcp.csproj src/Infosphere.Mcp/

RUN dotnet restore src/Infosphere.Mcp/Infosphere.Mcp.csproj

COPY src/Infosphere.Mcp/ src/Infosphere.Mcp/

RUN dotnet publish src/Infosphere.Mcp/Infosphere.Mcp.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Infosphere.Mcp.dll"]
