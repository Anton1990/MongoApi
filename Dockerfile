FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY Contracts/Contracts.csproj Contracts/
COPY Shared.Messaging/Shared.Messaging.csproj Shared.Messaging/
COPY MongoApi.csproj .
RUN dotnet restore MongoApi.csproj

# Copy source
COPY Contracts/ Contracts/
COPY . .
RUN dotnet publish MongoApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MongoApi.dll"]
