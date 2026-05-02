# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia solution e projetos
COPY FinTrackAI.sln .
COPY src/FinTrackAI.Domain/FinTrackAI.Domain.csproj src/FinTrackAI.Domain/
COPY src/FinTrackAI.Application/FinTrackAI.Application.csproj src/FinTrackAI.Application/
COPY src/FinTrackAI.Infrastructure/FinTrackAI.Infrastructure.csproj src/FinTrackAI.Infrastructure/
COPY src/FinTrackAI.API/FinTrackAI.API.csproj src/FinTrackAI.API/

# Restore
RUN dotnet restore

# Copia tudo e faz build
COPY . .
RUN dotnet publish src/FinTrackAI.API/FinTrackAI.API.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render injeta PORT em runtime; $$ vira $ no script visto pelo shell do container.
EXPOSE 8080
ENV PORT=8080

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet FinTrackAI.API.dll --urls http://0.0.0.0:$$PORT"]
