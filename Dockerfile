# --- BUILD STAGE ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy and restore
COPY ["playwright-screenshot/playwright-screenshot.csproj", "playwright-screenshot/"]
RUN dotnet restore "playwright-screenshot/playwright-screenshot.csproj"

# Build and publish
COPY . .
WORKDIR "/src/playwright-screenshot"
RUN dotnet publish "playwright-screenshot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- FINAL STAGE ---
FROM mcr.microsoft.com/playwright/dotnet:v1.57.0-noble AS final
WORKDIR /app

# Install the .NET 10 Runtime into this Playwright image
COPY --from=mcr.microsoft.com/dotnet/aspnet:10.0 /usr/share/dotnet /usr/share/dotnet

# Ensure the system knows where the dotnet executable is
ENV PATH="$PATH:/usr/share/dotnet"
ENV DOTNET_ROOT=/usr/share/dotnet

# Get the executable and copy it to /healthchecks
COPY --from=ghcr.io/alexaka1/distroless-dotnet-healthchecks:1 / /healthchecks
# Setup the healthcheck using the EXEC array syntax
HEALTHCHECK CMD ["/healthchecks/Distroless.HealthChecks", "--uri", "http://localhost:8080/health"]


# Set the environment variable so Playwright knows where to find browsers
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

COPY --from=build /app/publish .


ENTRYPOINT ["dotnet", "playwright-screenshot.dll"]