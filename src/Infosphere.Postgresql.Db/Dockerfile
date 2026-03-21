FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY global.json ./
COPY src/Infosphere.Postgresql.Db/Infosphere.Postgresql.Db.csproj src/Infosphere.Postgresql.Db/

RUN dotnet restore src/Infosphere.Postgresql.Db/Infosphere.Postgresql.Db.csproj

COPY src/Infosphere.Postgresql.Db/ src/Infosphere.Postgresql.Db/

RUN dotnet publish src/Infosphere.Postgresql.Db/Infosphere.Postgresql.Db.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Infosphere.Postgresql.Db.dll"]
