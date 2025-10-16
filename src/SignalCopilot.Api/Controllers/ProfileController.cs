using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SignalCopilot.Api.Models;
using System.Security.Claims;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's profile (risk profile, cash buffer)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserProfileDto
        {
            RiskProfile = user.RiskProfile.ToString(),
            CashBuffer = user.CashBuffer,
            CreatedAt = user.CreatedAt
        });
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        // Update risk profile
        if (!string.IsNullOrEmpty(request.RiskProfile))
        {
            if (Enum.TryParse<RiskProfile>(request.RiskProfile, out var riskProfile))
            {
                user.RiskProfile = riskProfile;
            }
            else
            {
                return BadRequest(new { message = "Invalid risk profile. Must be Conservative, Balanced, or Aggressive." });
            }
        }

        // Update cash buffer
        if (request.CashBuffer.HasValue)
        {
            if (request.CashBuffer.Value < 0)
            {
                return BadRequest(new { message = "Cash buffer cannot be negative." });
            }
            user.CashBuffer = request.CashBuffer.Value;
        }
        else if (request.ClearCashBuffer)
        {
            user.CashBuffer = null;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to update profile.", errors = result.Errors });
        }

        _logger.LogInformation("User {UserId} updated profile: RiskProfile={RiskProfile}, CashBuffer={CashBuffer}",
            userId, user.RiskProfile, user.CashBuffer);

        return Ok(new UserProfileDto
        {
            RiskProfile = user.RiskProfile.ToString(),
            CashBuffer = user.CashBuffer,
            CreatedAt = user.CreatedAt
        });
    }
}

// DTOs
public class UserProfileDto
{
    public string RiskProfile { get; set; } = "Balanced";
    public decimal? CashBuffer { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileRequest
{
    public string? RiskProfile { get; set; }
    public decimal? CashBuffer { get; set; }
    public bool ClearCashBuffer { get; set; } = false;
}
