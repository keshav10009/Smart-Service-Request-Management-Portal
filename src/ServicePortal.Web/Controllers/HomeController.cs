using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Web.Middleware;
using ServicePortal.Web.Models;

namespace ServicePortal.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Dashboard");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Demonstrates AC-11: Triggering a controlled unexpected error to demonstrate correlation ID logging
    /// without exposing sensitive database or internal details to the user.
    /// </summary>
    public IActionResult TriggerControlledError()
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationHeaderName]?.ToString();
        _logger.LogError("AC-11 Demonstration: Controlled simulated exception triggered. CorrelationId: {CorrelationId}", correlationId);
        throw new InvalidOperationException($"Controlled test exception for AC-11 validation (Correlation ID: {correlationId})");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationHeaderName]?.ToString()
            ?? HttpContext.Response.Headers[CorrelationIdMiddleware.CorrelationHeaderName].FirstOrDefault();

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            CorrelationId = correlationId
        });
    }
}
