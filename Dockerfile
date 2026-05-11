# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
 
COPY FinTrackAI.sln .
COPY src/FinTrackAI.Domain/FinTrackAI.Domain.csproj src/FinTrackAI.Domain/
COPY src/FinTrackAI.Application/FinTrackAI.Application.csproj src/FinTrackAI.Application/
COPY src/FinTrackAI.Infrastructure/FinTrackAI.Infrastructure.csproj src/FinTrackAI.Infrastructure/
COPY src/FinTrackAI.API/FinTrackAI.API.csproj src/FinTrackAI.API/
 
RUN dotnet restore
 
COPY . .
RUN dotnet publish src/FinTrackAI.API/FinTrackAI.API.csproj \
    -c Release -o /app/publish --no-restore
 
# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
 
COPY --from=build /app/publish .
 
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FinTrackAI.API.dll"]
 