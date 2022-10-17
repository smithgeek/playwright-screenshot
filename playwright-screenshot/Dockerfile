#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal AS base
WORKDIR /app
EXPOSE 80
# Install NodeJS to run playwright installer
RUN apt-get update -yq \
    && apt-get install curl gnupg -yq \
    && curl -sL https://deb.nodesource.com/setup_16.x | bash \
    && apt-get install nodejs -yq \
    && npm install -g npm
# Install browsers and depdencies for playwright
RUN npx playwright@1.27.1 install chromium \
    && npx playwright@1.27.1 install-deps chromium
#Uninstall NodeJS
RUN apt remove nodejs curl gnupg -yq \
    rm -fv /etc/apt/sources.list.d/nodesource*

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["playwright-screenshot/playwright-screenshot.csproj", "playwright-screenshot/"]
RUN dotnet restore "playwright-screenshot/playwright-screenshot.csproj"
COPY . .
WORKDIR "/src/playwright-screenshot"
RUN dotnet build "playwright-screenshot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "playwright-screenshot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "playwright-screenshot.dll"]