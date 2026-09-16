# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached when only source files change
COPY ShopLite.Api.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# .NET 8+ images listen on 8080 by default and include a non-root 'app' user
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShopLite.Api.dll"]
