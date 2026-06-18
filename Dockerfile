FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore
COPY src/InventoryHold.Contracts/InventoryHold.Contracts.csproj src/InventoryHold.Contracts/
COPY src/InventoryHold.Domain/InventoryHold.Domain.csproj src/InventoryHold.Domain/
COPY src/InventoryHold.Infrastructure/InventoryHold.Infrastructure.csproj src/InventoryHold.Infrastructure/
COPY src/InventoryHold.WebApi/InventoryHold.WebApi.csproj src/InventoryHold.WebApi/
RUN dotnet restore src/InventoryHold.WebApi/InventoryHold.WebApi.csproj

# Copy source code
COPY src/ src/

# Build and publish
RUN dotnet publish src/InventoryHold.WebApi/InventoryHold.WebApi.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5051
EXPOSE 5051
ENTRYPOINT ["dotnet", "InventoryHold.WebApi.dll"]
