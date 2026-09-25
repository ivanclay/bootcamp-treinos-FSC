## Git

- **SEMPRE** use [Conventional Commits](https://www.conventionalcommits.org/) para mensagens de commit. Exemplo: `feat: add start workout session endpoint`, `fix: workout plan validation`, `docs: update architecture rules`.
- **NUNCA** faça commit sem a permissão explícita do usuário. Sempre aguarde o usuário pedir para commitar.

## API: Controllers

- **SEMPRE** siga os princípios do REST. Exemplo: `GET /workout-plans`, `GET /workout-plans/{id}/days/{dayId}`.
- **SEMPRE** crie os controllers em `backend/FitAi.Api/Controllers`, com `[ApiController]`, `[Route]` e `[Tags]`.
- **SEMPRE** use os DTOs de `shared/FitAi.Contracts` para request e response. Validação de entrada com DataAnnotations nos DTOs.
- **SEMPRE** use os enums de `FitAi.Contracts` (`WeekDay`, `WorkoutGoal`, `UserRole`) — **NUNCA** `string` para esses campos.
- Erros de resposta **SEMPRE** no formato `ErrorResponse { error, code }` (códigos em `ErrorCodes`).
- Um controller **NUNCA** deve conter regras de negócio, apenas validação de entrada e autenticação/autorização.
- Rotas protegidas: `[Authorize]`; o usuário vem de `ICurrentUser` (papel e bloqueio lidos do banco). Rotas administrativas: `[RequireRoles(UserRole.ADMIN, UserRole.TEACHER)]`.
- Um controller deve **SEMPRE** chamar um use case (injetado com `[FromServices]`).
- Os controllers **NÃO** usam try/catch para erros de negócio: o use case lança uma exceção de `Errors/` e o `AppExceptionHandler` a converte no status HTTP correto.
- **SEMPRE** documente a ação com `/// <summary>` quando o comportamento não for óbvio (aparece no OpenAPI).

### Exemplo:

```csharp
[ApiController]
[Authorize]
[Route("workout-plans")]
[Tags("Workout Plan")]
public sealed class WorkoutPlansController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Inicia o treino do dia.</summary>
    [HttpPost("{workoutPlanId:guid}/days/{workoutDayId:guid}/sessions")]
    [ProducesResponseType<StartWorkoutSessionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> StartSession(
        Guid workoutPlanId, Guid workoutDayId, [FromServices] StartWorkoutSession startWorkoutSession, CancellationToken ct)
    {
        var result = await startWorkoutSession.ExecuteAsync(
            new StartWorkoutSession.Input(currentUser.UserId, workoutPlanId, workoutDayId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
```

## API: Use Cases

- Todas as regras de negócio devem estar concentradas dentro de um use case.
- Todos os use cases ficam em `backend/FitAi.Api/UseCases/<Área>/`, um por arquivo.
- Todos os use cases são classes `sealed` com um método `ExecuteAsync`, nomeadas com verbos.
- O parâmetro de entrada é **SEMPRE** um record `Input` aninhado na classe.
- O retorno é **SEMPRE** um DTO de `FitAi.Contracts` (ou um record `Output` aninhado quando for interno à API). **NUNCA** devolva a entidade do EF Core.
- Ao precisar do banco, o use case usa o `AppDbContext` diretamente (sem repository).
- Atomicidade: prefira uma única chamada a `SaveChangesAsync` (que já é transacional).
- **NUNCA** trate erros nos use cases. Lance uma exceção de `Errors/AppExceptions.cs`; se a necessária não existir, crie-a (com status HTTP e código).
- Mensagens de erro de validação/conflito em português (são exibidas na Web e no App).
- Use cases são registrados automaticamente no DI (qualquer classe em `FitAi.Api.UseCases` com `ExecuteAsync`).

### Exemplo:

```csharp
public sealed class StartWorkoutSession(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(string UserId, Guid WorkoutPlanId, Guid WorkoutDayId);

    public async Task<StartWorkoutSessionResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await db.WorkoutPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == input.WorkoutPlanId && p.UserId == input.UserId, ct)
            ?? throw new NotFoundException("Workout plan not found");
        if (!plan.IsActive) throw new WorkoutPlanNotActiveException("Workout plan is not active");

        var session = new WorkoutSession { WorkoutDayId = input.WorkoutDayId, StartedAt = timeProvider.GetUtcNow() };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new StartWorkoutSessionResponse(session.Id);
    }
}
```

## Web (MVC)

- A Web **NUNCA** acessa o banco: toda leitura/escrita passa pelo `ApiClient` (HTTP para a API).
- Telas do aluno em `Controllers/` + `Views/`; área administrativa em `Areas/Admin`.
- Formulários POST sempre com antiforgery (`asp-antiforgery="true"` quando o `action` é literal).
- Erros da API são tratados pelo `ApiExceptionFilter`; capture `ApiException` na action só para mostrar mensagens de validação no próprio formulário.

## App (MAUI)

- MVVM com CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`); páginas com `x:DataType` (bindings compiladas).
- Toda chamada à API passa por `Services/ApiClient`; token no `SecureStorage` via `AuthService`.
