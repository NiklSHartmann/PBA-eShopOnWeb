FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Packages.props global.json ./
COPY src/Services/Catalog.API/Catalog.API.csproj src/Services/Catalog.API/
RUN dotnet restore src/Services/Catalog.API/Catalog.API.csproj

COPY src/Services/Catalog.API/ src/Services/Catalog.API/
RUN dotnet publish src/Services/Catalog.API/Catalog.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Catalog.API.dll"]
