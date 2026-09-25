# Build a partir da raiz do repositório:
#   docker build -f backend/FitAi.Api/Dockerfile -t fitai-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/FitAi.Contracts/FitAi.Contracts.csproj shared/FitAi.Contracts/
COPY backend/FitAi.Api/FitAi.Api.csproj backend/FitAi.Api/
RUN dotnet restore backend/FitAi.Api/FitAi.Api.csproj
COPY shared/ shared/
COPY backend/ backend/
RUN dotnet publish backend/FitAi.Api/FitAi.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FitAi.Api.dll"]
