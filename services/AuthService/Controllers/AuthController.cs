using AuthService.Data;
using AuthService.Application.DTOs;
using AuthService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthService.Services;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly TokenService _tokenService;

    public AuthController(
    AuthDbContext dbContext,
    PasswordService passwordService,
    TokenService tokenService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,

            PasswordHash = _passwordService.HashPassword(request.Password),

            Role = "Customer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "User registered successfully.",
            user = new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "This account is inactive."
            });
        }

        var passwordValid = _passwordService.VerifyPassword(
            request.Password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var token = _tokenService.GenerateAccessToken(user);

        return Ok(new
        {
            message = "Login successful.",
            token,
            user = new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role
            }
        });
    }
}