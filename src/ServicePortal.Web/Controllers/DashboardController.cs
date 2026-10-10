using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Administrator))
        {
            return RedirectToAction(nameof(Admin));
        }
        if (User.IsInRole(AppRoles.TeamLead))
        {
            return RedirectToAction(nameof(TeamLead));
        }
        if (User.IsInRole(AppRoles.SupportAgent))
        {
            return RedirectToAction(nameof(Agent));
        }

        return RedirectToAction(nameof(Requester));
    }

    [HttpGet]
    public async Task<IActionResult> Requester()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _dashboardService.GetRequesterDashboardAsync(userId);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.SupportAgent},{AppRoles.TeamLead},{AppRoles.Administrator}")]
    public async Task<IActionResult> Agent()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _dashboardService.GetAgentDashboardAsync(userId);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.TeamLead},{AppRoles.Administrator}")]
    public async Task<IActionResult> TeamLead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _dashboardService.GetTeamLeadDashboardAsync(userId);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> Admin()
    {
        var model = await _dashboardService.GetAdminDashboardAsync();
        return View(model);
    }
}
