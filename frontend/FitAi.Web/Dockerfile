# Build a partir da raiz do repositório:
#   docker build -f frontend/FitAi.Web/Dockerfile -t fitai-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/FitAi.Contracts/FitAi.Contracts.csproj shared/FitAi.Contracts/
COPY frontend/FitAi.Web/FitAi.Web.csproj frontend/FitAi.Web/
RUN dotnet restore frontend/FitAi.Web/FitAi.Web.csproj
COPY shared/ shared/
COPY frontend/ frontend/
RUN dotnet publish frontend/FitAi.Web/FitAi.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:3000
EXPOSE 3000
ENTRYPOINT ["dotnet", "FitAi.Web.dll"]
