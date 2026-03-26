# Use the official .NET 9 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the csproj and restore dependencies
COPY ["DataDriveApi.csproj", "./"]
RUN dotnet restore "DataDriveApi.csproj"

# Copy the rest of the code and build
COPY . .
RUN dotnet build "DataDriveApi.csproj" -c Release -o /app/build

# Publish the application (creates optimized binaries)
FROM build AS publish
RUN dotnet publish "DataDriveApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the lightweight ASP.NET runtime image for the final container
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Render assigns a dynamic port via the $PORT environment variable.
# We map it to ASP.NET's recognized environment variable so it binds correctly.
# Expose port as documentation
EXPOSE 8080

ENTRYPOINT ["dotnet", "DataDriveApi.dll"]
