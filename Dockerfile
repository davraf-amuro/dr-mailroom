# dr-mailroom-api — immagine multi-stage .NET 10 (host unico: API REST + scheduler Quartz)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Prima i soli file di progetto: il restore resta in cache finché le dipendenze non cambiano
COPY global.json Directory.Build.props dr-mailroom.slnx ./
COPY src/dr-mailroom-core/dr-mailroom-core.csproj src/dr-mailroom-core/
COPY src/dr-mailroom-api/dr-mailroom-api.csproj src/dr-mailroom-api/
RUN dotnet restore src/dr-mailroom-api/dr-mailroom-api.csproj

COPY src/ src/
RUN dotnet publish src/dr-mailroom-api/dr-mailroom-api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Percorsi dei volumi: allegati, key ring Data Protection (fuori dal DB, T6) e log Serilog
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Mailroom__Attachments__RootPath=/data/attachments \
    Mailroom__DataProtection__KeyRingPath=/data/keys

RUN mkdir -p /data/attachments /data/keys /app/logs \
    && chown -R app:app /data /app/logs

COPY --from=build --chown=app:app /app/publish .

# Utente non root fornito dall'immagine aspnet
USER app
EXPOSE 8080
VOLUME ["/data/attachments", "/data/keys", "/app/logs"]

ENTRYPOINT ["dotnet", "dr-mailroom-api.dll"]
