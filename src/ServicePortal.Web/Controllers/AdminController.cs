using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Web.Controllers;

[Authorize(Roles = AppRoles.Administrator)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users
            .Include(u => u.SupportTeam)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var userList = new List<AdminUserViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            userList.Add(new AdminUserViewModel
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                Department = u.Department,
                TeamName = u.SupportTeam?.Name,
                IsActive = u.IsActive,
                EmailConfirmed = u.EmailConfirmed,
                Roles = roles.ToList()
            });
        }

        ViewBag.AllRoles = AppRoles.AllRoles;
        return View(userList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserActive(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = $"User {user.DisplayName} has been {(user.IsActive ? "activated" : "deactivated")}.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyUserEmail(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = $"Email for {user.DisplayName} has been marked as verified.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(string userId, string newRole)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && AppRoles.AllRoles.Contains(newRole))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);
            TempData["SuccessMessage"] = $"Assigned role '{newRole}' to {user.DisplayName}.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Teams()
    {
        var teams = await _context.SupportTeams
            .Include(t => t.Members)
            .Include(t => t.Categories)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(teams);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(string name, string? description)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _context.SupportTeams.Add(new SupportTeam
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Support team '{name}' created.";
        }
        return RedirectToAction(nameof(Teams));
    }

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.Categories
            .Include(c => c.SupportTeam)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var teams = await _context.SupportTeams.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        ViewBag.Teams = new SelectList(teams, "Id", "Name");

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string? description, int supportTeamId)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _context.Categories.Add(new Category
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                SupportTeamId = supportTeamId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Category '{name}' created and routed to support team.";
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> SlaPolicies()
    {
        var policies = await _context.SlaPolicies
            .OrderBy(p => p.Priority)
            .ToListAsync();

        return View(policies);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSlaPolicy(int id, int responseHours, int resolutionHours)
    {
        var policy = await _context.SlaPolicies.FindAsync(id);
        if (policy != null && responseHours > 0 && resolutionHours > 0)
        {
            policy.ResponseTargetHours = responseHours;
            policy.ResolutionTargetHours = resolutionHours;
            policy.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"SLA policy for {policy.Priority} updated.";
        }
        return RedirectToAction(nameof(SlaPolicies));
    }

    [HttpGet]
    public async Task<IActionResult> AuditLogs(string? search, string? correlationId, int page = 1)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.ActorName.Contains(search) || a.Action.Contains(search) || a.EntityName.Contains(search) || a.Details.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(a => a.CorrelationId == correlationId);
        }

        const int pageSize = 20;
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.Search = search;
        ViewBag.CorrelationId = correlationId;

        return View(items);
    }
}

public class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? TeamName { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = [];
}
