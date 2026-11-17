# ---- Build ----------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached when only code changes.
COPY nuget.config ./
COPY src/Portfolio.Web/Portfolio.Web.csproj src/Portfolio.Web/
RUN dotnet restore src/Portfolio.Web/Portfolio.Web.csproj

COPY src/ src/
RUN dotnet publish src/Portfolio.Web/Portfolio.Web.csproj -c Release -o /app --no-restore

# ---- Run ------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Render sends traffic to port 10000 by default.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

# Run as the image's built-in non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "Portfolio.Web.dll"]
