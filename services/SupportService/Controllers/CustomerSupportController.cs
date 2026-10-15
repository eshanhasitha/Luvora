using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportService.Data;
using SupportService.Models;

namespace SupportService.Controllers;

[ApiController]
[Authorize]
[Route("api/support")]
public sealed class CustomerSupportController(SupportDbContext db) : ControllerBase
{
    private Guid? CustomerId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("categories")]
    public IActionResult Categories() => Ok(new[] { "Order", "Payment", "Delivery", "Product", "Account", "Other" });

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CustomerTicketRequest request)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Subject and message are required." });
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket { Id = Guid.NewGuid(), CustomerId = customerId, Subject = request.Subject.Trim(), Category = request.Category?.Trim() ?? "Other", Priority = "Normal", CreatedAt = now, UpdatedAt = now };
        db.Tickets.Add(ticket);
        db.Messages.Add(new TicketMessage { Id = Guid.NewGuid(), TicketId = ticket.Id, AuthorId = customerId, Body = request.Message.Trim(), CreatedAt = now, IsInternal = false });
        await db.SaveChangesAsync();
        return Created($"/api/support/tickets/{ticket.Id}", ticket);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> ListTickets()
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        return Ok(await db.Tickets.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId && x.CustomerId == customerId);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid ticketId)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        if (!await db.Tickets.AnyAsync(x => x.Id == ticketId && x.CustomerId == customerId)) return NotFound();
        return Ok(await db.Messages.AsNoTracking().Where(x => x.TicketId == ticketId && !x.IsInternal).OrderBy(x => x.CreatedAt).ToListAsync());
    }

    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> Reply(Guid ticketId, CustomerMessageRequest request)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Body)) return BadRequest(new { message = "Message body is required." });
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId && x.CustomerId == customerId);
        if (ticket is null) return NotFound();
        if (ticket.Status is "Closed" or "Resolved") return Conflict(new { message = "This ticket is closed." });
        var message = new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorId = customerId, Body = request.Body.Trim(), IsInternal = false, CreatedAt = DateTime.UtcNow };
        db.Messages.Add(message); ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status == "WaitingCustomer") ticket.Status = "InProgress";
        await db.SaveChangesAsync(); return Created($"/api/support/tickets/{ticketId}/messages/{message.Id}", message);
    }

    [HttpPost("tickets/{ticketId:guid}/close")]
    public async Task<IActionResult> Close(Guid ticketId)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId && x.CustomerId == customerId);
        if (ticket is null) return NotFound();
        ticket.Status = "Closed"; ticket.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("complaints")]
    public async Task<IActionResult> Complaint(CustomerComplaintRequest request)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description)) return BadRequest(new { message = "Subject and description are required." });
        var item = new Complaint { Id = Guid.NewGuid(), CustomerId = customerId, OrderId = request.OrderId, Subject = request.Subject.Trim(), Description = request.Description.Trim(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Complaints.Add(item); await db.SaveChangesAsync(); return Created($"/api/support/complaints/{item.Id}", item);
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> MyComplaints()
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        return Ok(await db.Complaints.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [HttpGet("complaints/{complaintId:guid}")]
    public async Task<IActionResult> MyComplaint(Guid complaintId)
    {
        if (CustomerId is not Guid customerId) return Unauthorized();
        var item = await db.Complaints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == complaintId && x.CustomerId == customerId);
        return item is null ? NotFound() : Ok(item);
    }

    public sealed record CustomerTicketRequest(string Subject, string Message, string? Category = null);
    public sealed record CustomerMessageRequest(string Body);
    public sealed record CustomerComplaintRequest(string Subject, string Description, Guid? OrderId = null);
}
