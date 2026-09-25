using FitAi.Api.Entities;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.Data;

/// <summary>
/// Cria usuários e dados de exemplo para explorar o sistema em desenvolvimento (Database:SeedDemoData).
/// Idempotente: não faz nada se o professor de exemplo já existir.
/// </summary>
public static class DemoDataSeeder
{
    public const string AdminEmail = "admin@fitai.local";
    public const string TeacherEmail = "professor@fitai.local";
    public const string StudentEmail = "aluno@fitai.local";
    public const string NewStudentEmail = "novo.aluno@fitai.local";
    public const string InviteCode = "FIT-DEMO26";

    private const string Upper1 = "/covers/upper-1.jpg";
    private const string Upper2 = "/covers/upper-3.jpg";
    private const string Lower1 = "/covers/lower-1.jpg";
    private const string Lower2 = "/covers/lower-4.jpg";
    private const string PlanCover = "/covers/plan.jpg";

    public static async Task SeedAsync(AppDbContext db, TimeProvider timeProvider, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == TeacherEmail, ct)) return;

        var now = timeProvider.GetUtcNow();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail, ct)
            ?? db.Users.Add(new User { Email = AdminEmail, Name = "Admin FIT.AI" }).Entity;
        admin.Role = UserRole.ADMIN;

        var teacher = db.Users.Add(new User { Email = TeacherEmail, Name = "Paulo Professor", Role = UserRole.TEACHER }).Entity;

        var student = db.Users.Add(new User
        {
            Email = StudentEmail,
            Name = "Ana Aluna",
            Teacher = teacher,
            WeightInGrams = 62500,
            HeightInCentimeters = 165,
            Age = 28,
            BodyFatPercentage = 24,
            CreatedAt = now.AddDays(-120),
        }).Entity;

        var plan = new WorkoutPlan
        {
            Name = "Hipertrofia — Upper/Lower",
            User = student,
            Goal = WorkoutGoal.HYPERTROPHY,
            CoverImageUrl = PlanCover,
            IsActive = true,
            Source = WorkoutPlanSource.TEACHER,
            CreatedBy = teacher,
            CreatedAt = now.AddDays(-120),
            WorkoutDays =
            [
                Training(WeekDay.MONDAY, "Superiores A — Peito e Costas", Upper1, ("Supino Inclinado", 4, 10), ("Remada Curvada", 4, 10), ("Desenvolvimento", 3, 12), ("Tríceps Corda", 3, 12)),
                Training(WeekDay.TUESDAY, "Inferiores A — Quadríceps", Lower1, ("Agachamento Livre", 4, 8), ("Leg Press", 4, 12), ("Cadeira Extensora", 3, 12), ("Panturrilha em Pé", 4, 15)),
                Rest(WeekDay.WEDNESDAY),
                Training(WeekDay.THURSDAY, "Superiores B — Ombros e Braços", Upper2, ("Puxada Frontal", 4, 10), ("Supino Reto", 4, 10), ("Elevação Lateral", 3, 15), ("Rosca Direta", 3, 12)),
                Training(WeekDay.FRIDAY, "Inferiores B — Posterior e Glúteos", Lower2, ("Levantamento Terra Romeno", 4, 10), ("Mesa Flexora", 3, 12), ("Elevação Pélvica", 4, 12), ("Abdução", 3, 15)),
                Rest(WeekDay.SATURDAY),
                Rest(WeekDay.SUNDAY),
            ],
        };
        db.WorkoutPlans.Add(plan);

        // Histórico: ~85% dos treinos dos últimos 4 meses concluídos; a semana atual completa até ontem (sequência ativa).
        var random = new Random(26);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        for (var offset = 118; offset >= 1; offset--)
        {
            var date = today.AddDays(-offset);
            var day = plan.WorkoutDays.First(d => d.WeekDay == Domain.WorkoutStreak.GetWeekDay(date));
            if (day.IsRest) continue;
            var recent = offset <= 14;
            if (!recent && random.NextDouble() > 0.85) continue;

            var startedAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero).AddMinutes(random.Next(0, 480));
            var completed = recent || random.NextDouble() > 0.1;
            day.Sessions.Add(new WorkoutSession
            {
                StartedAt = startedAt,
                CompletedAt = completed ? startedAt.AddMinutes(random.Next(40, 70)) : null,
            });
        }

        db.EmailInvites.Add(new EmailInvite { Email = NewStudentEmail, Role = UserRole.STUDENT, Teacher = teacher, InvitedBy = teacher });
        db.InviteCodes.Add(new InviteCode { Code = InviteCode, Teacher = teacher, ExpiresAt = now.AddDays(365) });

        await db.SaveChangesAsync(ct);
    }

    private static WorkoutDay Training(WeekDay weekDay, string name, string cover, params (string Name, int Sets, int Reps)[] exercises) => new()
    {
        WeekDay = weekDay,
        Name = name,
        CoverImageUrl = cover,
        EstimatedDurationInSeconds = 55 * 60,
        Exercises = exercises.Select((e, i) => new WorkoutExercise
        {
            Order = i, Name = e.Name, Sets = e.Sets, Reps = e.Reps, RestTimeInSeconds = e.Reps <= 8 ? 120 : 60,
        }).ToList(),
    };

    private static WorkoutDay Rest(WeekDay weekDay) => new() { WeekDay = weekDay, Name = "Descanso", IsRest = true, CoverImageUrl = Upper1 };
}
