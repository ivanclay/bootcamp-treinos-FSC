using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class ListUsers(AppDbContext db)
{
    public sealed record Input(User Actor, string? Search, UserRole? Role, string? TeacherId, int Page, int PageSize);

    public async Task<PagedResponse<AdminUserListItemResponse>> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);

        var query = db.Users.AsNoTracking().ManagedBy(input.Actor);
        if (input.Role is { } role) query = query.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(input.TeacherId)) query = query.Where(u => u.TeacherId == input.TeacherId);
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var pattern = $"%{input.Search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AdminProjections.UserListItem)
            .ToListAsync(ct);
        return new PagedResponse<AdminUserListItemResponse>(items, page, pageSize, total);
    }
}
