# CLAUDE.md

Este arquivo orienta o Claude Code (claude.ai/code) ao trabalhar com o codigo deste repositorio.

## Visao Geral

FIT.AI em .NET 10: API REST (ASP.NET Core com controllers), front-end web (ASP.NET Core MVC com Razor Views, incluindo a area administrativa `/admin`) e app Android (.NET MAUI). Banco PostgreSQL via EF Core. Coach AI com `Microsoft.Extensions.AI` (OpenAI por padrao, provedor configuravel).

## Projetos

| Pasta | Projeto | Papel |
| --- | --- | --- |
| `backend/FitAi.Api` | ASP.NET Core Web API | Regras de negocio, banco, autenticacao, Coach AI |
| `frontend/FitAi.Web` | ASP.NET Core MVC | Telas do aluno + Area `Admin` (professor/admin); consome a API via HTTP |
| `app/FitAi.App` | .NET MAUI (Android) | App do aluno; consome a API via HTTP |
| `shared/FitAi.Contracts` | Class library | DTOs de request/response e enums compartilhados |
| `tests/FitAi.Api.Tests` | xUnit | Testes da API |
| `tests/FitAi.Web.Tests` | xUnit | Testes da Web |

## Comandos

```bash
# PostgreSQL
docker compose up -d

# API (http://localhost:8080, docs em /docs) — aplica migrations no startup em Development
dotnet run --project backend/FitAi.Api

# Web (http://localhost:3000)
dotnet run --project frontend/FitAi.Web

# Testes
dotnet test tests/FitAi.Api.Tests && dotnet test tests/FitAi.Web.Tests

# Nova migration (requer: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Nome> --project backend/FitAi.Api -o Data/Migrations

# App: abrir app/FitAi.App no Visual Studio/Rider com o workload maui-android e o Android SDK.
# Checagem de compilacao sem Android SDK (valida C# e bindings do XAML):
dotnet build app/FitAi.App -p:TargetFrameworks=net10.0 -p:OutputType=Library -p:MauiXamlInflator=XamlC
```

## Arquitetura da API

### Camadas: Controllers → Use Cases → EF Core

- **Controllers** (`Controllers/`) — Validam a entrada (DataAnnotations nos DTOs de `FitAi.Contracts`), obtem o usuario autenticado via `ICurrentUser` e chamam **um** use case. Nenhuma regra de negocio.
- **Use Cases** (`UseCases/<Area>/`) — Uma classe por caso de uso, nomeada com verbo, com `ExecuteAsync(Input)`. Recebem `AppDbContext` por injecao, falam direto com o EF Core e devolvem DTOs de `FitAi.Contracts` (nunca entidades). Registrados automaticamente no DI (`Program.cs`).
- **Entities** (`Entities/`) e **Data** (`Data/AppDbContext.cs`, `Data/Migrations/`) — Modelo do EF Core. Enums salvos como texto.
- **Errors** (`Errors/`) — Excecoes de negocio (`NotFoundException`, `ConflictException`...) convertidas em `{ error, code }` pelo `AppExceptionHandler`.
- **Domain** (`Domain/WorkoutStreak.cs`) — Calculo puro da sequencia.
- **Ai** (`Ai/`) — Prompt, fabrica de `IChatClient` por provedor e configuracoes editaveis no painel.

### Autenticacao e papeis

Login Google feito pela API → redirect para o cliente com codigo de uso unico → `POST /auth/token` devolve um JWT. Papeis: `ADMIN` (e-mails em `Auth:AdminEmails`), `TEACHER` e `STUDENT`. Papel e bloqueio sao lidos do banco a cada requisicao (`ICurrentUser`, `RequireRolesAttribute`). Professor so enxerga os proprios alunos (`StudentAccess`). Em Development existe `POST /auth/dev-login`.

### Web

Cookie de login guarda o JWT (claim `access_token`); `ApiClient` + `BearerTokenHandler` chamam a API. Erros da API viram paginas amigaveis (`ApiExceptionFilter`). Area administrativa em `Areas/Admin` (Bootstrap); telas do aluno com CSS proprio (`wwwroot/css/site.css`).

## Convencoes

- C# com `Nullable` e `ImplicitUsings` habilitados; records para DTOs.
- Datas em UTC na API (`DateTimeOffset`/`timestamptz`); a Web converte para `App:TimeZone`.
- Mensagens de erro de validacao da API em portugues (sao exibidas nas telas).
- Segredos (JWT, Google, chaves de IA, YouTube) em User Secrets ou variaveis de ambiente, nunca no `appsettings.json`.
- Seguranca: ver `SECURITY.md`. Login exige PKCE; rotas sensiveis usam `[EnableRateLimiting]`; texto vindo da IA e sempre sanitizado antes de virar HTML.
