using System.Linq.Expressions;
using FitAi.Api.Entities;
using FitAi.Contracts;

namespace FitAi.Api.UseCases.Admin;

public static class AdminProjections
{
    public static readonly Expression<Func<User, AdminUserListItemResponse>> UserListItem = u =>
        new AdminUserListItemResponse(
            u.Id,
            u.Name,
            u.Email,
            u.Image,
            u.Role,
            u.TeacherId,
            u.Teacher != null ? u.Teacher.Name : null,
            u.IsBlocked,
            u.AllowAiWorkoutPlans,
            u.WorkoutPlans.Any(p => p.IsActive),
            u.WorkoutPlans.SelectMany(p => p.WorkoutDays).SelectMany(d => d.Sessions).Max(s => (DateTimeOffset?)s.StartedAt),
            u.CreatedAt);
}
