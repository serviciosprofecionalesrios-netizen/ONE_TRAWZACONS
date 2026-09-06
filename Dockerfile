FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ITServiceDeskApp.csproj ./
RUN dotnet restore ITServiceDeskApp.csproj

COPY . ./
RUN dotnet publish ITServiceDeskApp.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends fontconfig tzdata \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=10000 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=America/Managua

COPY --from=build /app/publish ./
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN sed -i 's/\r$//' /app/docker-entrypoint.sh \
    && chmod +x /app/docker-entrypoint.sh \
    && mkdir -p /app/storage \
    && chown -R app:app /app

USER app
EXPOSE 10000

ENTRYPOINT ["/app/docker-entrypoint.sh"]
