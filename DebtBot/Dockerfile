#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# The web UI (DebtBot/ClientApp): built here and copied into wwwroot of the final image.
FROM node:22-alpine AS client
WORKDIR /src/DebtBot/ClientApp
COPY ["DebtBot/ClientApp/package.json", "DebtBot/ClientApp/package-lock.json", "./"]
RUN npm ci
COPY DebtBot/ClientApp/ ./
# vite writes to ../wwwroot, i.e. /src/DebtBot/wwwroot
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY ["DebtBot/DebtBot.csproj", "DebtBot/"]
RUN dotnet restore "DebtBot/DebtBot.csproj"
COPY . .
WORKDIR "/src/DebtBot"
RUN dotnet build "DebtBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DebtBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=client /src/DebtBot/wwwroot ./wwwroot
ENTRYPOINT ["dotnet", "DebtBot.dll"]