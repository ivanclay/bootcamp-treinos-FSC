# Regras gerais

## Stack

- .NET 10 (C#), solution `FitAi.slnx`
- ASP.NET Core Web API com controllers (`backend/FitAi.Api`)
- ASP.NET Core MVC com Razor Views (`frontend/FitAi.Web`)
- .NET MAUI para Android (`app/FitAi.App`), MVVM com CommunityToolkit.Mvvm
- EF Core 10 + Npgsql (PostgreSQL 16)
- Microsoft.Extensions.AI (Coach AI), OpenAI por padrão e provedores configuráveis
- xUnit para testes

## Comandos

```bash
docker compose up -d                              # PostgreSQL
dotnet run --project backend/FitAi.Api            # API em http://localhost:8080 (docs em /docs)
dotnet run --project frontend/FitAi.Web           # Web em http://localhost:3000
dotnet test tests/FitAi.Api.Tests                 # Testes
dotnet ef migrations add <Nome> --project backend/FitAi.Api -o Data/Migrations
```

## Estrutura da API

- `Controllers/` - Controllers (uma responsabilidade por controller/área)
- `UseCases/<Área>/` - Classes de regra de negócio (padrão use case)
- `Entities/` - Entidades do EF Core
- `Data/` - `AppDbContext` e migrations
- `Errors/` - Exceções de negócio e o handler que as converte em `{ error, code }`
- `Domain/` - Lógica pura (ex.: cálculo da sequência)
- `Ai/` - Prompt e fábrica de `IChatClient`
- `Options/` - Classes de configuração (`Auth`, `Ai`, `YouTube`)

DTOs de request/response ficam em `shared/FitAi.Contracts` e são usados pela API, pela Web e pelo App.

## Documentação da API

OpenAPI em `/swagger.json` e Scalar em `/docs` quando a API está rodando.

## MCPs

- **SEMPRE** use Context7 para buscar documentações
- **SEMPRE** use Serena para semantic code retrieval e editing tools.
