using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Web.Controllers;

[Authorize]
public class ServiceRequestController : Controller
{
    private readonly IServiceRequestService _requestService;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ServiceRequestController> _logger;

    public ServiceRequestController(
        IServiceRequestService requestService,
        IFileStorageService fileStorage,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<ServiceRequestController> logger)
    {
        _requestService = requestService;
        _fileStorage = fileStorage;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RequestFilterDto filter)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var result = await _requestService.SearchRequestsAsync(filter, userId, roles);

        await PopulateFilterDropdownsAsync(filter);
        ViewBag.Filter = filter;

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var request = await _requestService.GetByIdAsync(id, userId, roles);
        if (request == null)
        {
            _logger.LogWarning("Access to Request {Id} denied or not found for user {UserId}", id, userId);
            return NotFound("The requested service request could not be found or you do not have permission to view it.");
        }

        // Populate eligible assignees if viewer is Team Lead or Admin (AC-05)
        var isLeadOrAdmin = roles.Contains(AppRoles.TeamLead) || roles.Contains(AppRoles.Administrator);
        if (isLeadOrAdmin)
        {
            var eligibleAgents = await _context.Users
                .Where(u => u.SupportTeamId == request.SupportTeamId && u.IsActive)
                .Select(u => new { u.Id, u.DisplayName })
                .ToListAsync();

            ViewBag.EligibleAgents = new SelectList(eligibleAgents, "Id", "DisplayName", request.AssigneeId);
        }

        ViewBag.CurrentUserId = userId;
        ViewBag.UserRoles = roles;
        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null && !user.EmailConfirmed)
        {
            ViewBag.IsEmailUnverified = true;
        }

        await PopulateCategoriesDropdownAsync();
        return View(new CreateRequestDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestDto model, List<IFormFile>? attachments)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Section 6.1 & AC-01: Require email verification before submitting
        if (!user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Email verification is required before submitting a service request. Please confirm your email in account settings.");
            ViewBag.IsEmailUnverified = true;
            await PopulateCategoriesDropdownAsync();
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesDropdownAsync();
            return View(model);
        }

        // Process attachments safely
        var fileStreams = new List<(Stream Stream, string FileName, string ContentType)>();
        if (attachments != null)
        {
            foreach (var file in attachments)
            {
                if (file.Length > 0)
                {
                    if (!_fileStorage.IsAllowedExtension(file.FileName))
                    {
                        ModelState.AddModelError(string.Empty, $"Attachment '{file.FileName}' has an unsupported file type. Allowed: {string.Join(", ", _fileStorage.AllowedExtensions)}");
                        await PopulateCategoriesDropdownAsync();
                        return View(model);
                    }
                    if (!_fileStorage.IsAllowedSize(file.Length))
                    {
                        ModelState.AddModelError(string.Empty, $"Attachment '{file.FileName}' exceeds the maximum allowed size of 10 MB.");
                        await PopulateCategoriesDropdownAsync();
                        return View(model);
                    }
                    fileStreams.Add((file.OpenReadStream(), file.FileName, file.ContentType));
                }
            }
        }

        var (success, error, requestId, reference) = await _requestService.CreateRequestAsync(
            model, user.Id, fileStreams);

        if (!success)
        {
            if (error != null && error.Contains("duplicate warning", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.DuplicateWarning = error;
            }
            ModelState.AddModelError(string.Empty, error ?? "Failed to create service request.");
            await PopulateCategoriesDropdownAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Service Request {reference} created successfully!";
        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(int id, StatusTransitionDto dto)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var result = await _requestService.TransitionStatusAsync(id, dto.TargetStatus, userId, roles, dto);

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = $"Status updated to {dto.TargetStatus} successfully.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.TeamLead},{AppRoles.Administrator}")]
    public async Task<IActionResult> Assign(int id, string targetAgentId, string? reason)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var result = await _requestService.AssignRequestAsync(id, targetAgentId, userId, roles, reason);

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Request assigned successfully.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, AddCommentDto dto)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var result = await _requestService.AddCommentAsync(id, dto, userId, roles);

        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Comment added successfully.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id, string storedName, string originalName)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        // Verify authorized access to request
        var request = await _requestService.GetByIdAsync(id, userId, roles);
        if (request == null)
        {
            return Forbid();
        }

        var fileResult = await _fileStorage.GetFileAsync(storedName);
        if (fileResult == null)
        {
            return NotFound("Attachment file not found.");
        }

        return File(fileResult.Value.FileStream, fileResult.Value.ContentType, originalName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] RequestFilterDto filter)
    {
        var userId = GetCurrentUserId();
        var roles = await GetCurrentUserRolesAsync();

        var csvBytes = await _requestService.ExportCsvAsync(filter, userId, roles);
        var fileName = $"ServiceRequests_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(csvBytes, "text/csv", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> AddCorrectionNotes(int id, string correctionNotes)
    {
        var userId = GetCurrentUserId();
        var success = await _requestService.AddCorrectionNotesAsync(id, correctionNotes, userId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Could not apply correction notes. Ensure the request is closed.";
        }
        else
        {
            TempData["SuccessMessage"] = "Administrator correction notes saved.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> CheckDuplicate(int categoryId, string title)
    {
        var result = await _requestService.CheckDuplicateAsync(categoryId, title);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulateEmailVerification()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Your email has been successfully verified! You can now submit service requests.";
        }
        return RedirectToAction(nameof(Create));
    }

    #region Helpers

    private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<IList<string>> GetCurrentUserRolesAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
    }

    private async Task PopulateCategoriesDropdownAsync()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name");
    }

    private async Task PopulateFilterDropdownsAsync(RequestFilterDto filter)
    {
        var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var teams = await _context.SupportTeams.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name", filter.CategoryId);
        ViewBag.Teams = new SelectList(teams, "Id", "Name", filter.TeamId);
    }

    #endregion
}
