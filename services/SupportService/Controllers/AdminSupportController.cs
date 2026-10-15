using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportService.Data;
using SupportService.Models;

namespace SupportService.Controllers;

[ApiController]
[Route("api/admin/support")]
public sealed class AdminSupportController(SupportDbContext db) : ControllerBase
{
    [HttpGet("tickets")]
    public async Task<IActionResult> ListTickets([FromQuery] string? status, [FromQuery] Guid? customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Tickets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        var count = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = count, tickets = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateTicketRequest request)
    {
        if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Subject)) return BadRequest(new { message = "Customer and subject are required." });
        if (request.Priority is not ("Low" or "Normal" or "High" or "Urgent")) return BadRequest(new { message = "Invalid ticket priority." });
        var ticket = new SupportTicket { Id = Guid.NewGuid(), CustomerId = request.CustomerId, Subject = request.Subject.Trim(), Category = request.Category?.Trim() ?? "General", Priority = request.Priority, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Tickets.Add(ticket);
        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            if (request.AuthorId == Guid.Empty) return BadRequest(new { message = "An author ID is required when an initial message is supplied." });
            db.Messages.Add(new TicketMessage { Id = Guid.NewGuid(), TicketId = ticket.Id, AuthorId = request.AuthorId, Body = request.InitialMessage.Trim(), IsInternal = false, CreatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(); return Created($"/api/admin/support/tickets/{ticket.Id}", ticket);
    }

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> GetTicket(Guid ticketId)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPatch("tickets/{ticketId:guid}")]
    public async Task<IActionResult> UpdateTicket(Guid ticketId, UpdateTicketRequest request)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null) return NotFound();
        if (request.Priority is not null && request.Priority is not ("Low" or "Normal" or "High" or "Urgent")) return BadRequest(new { message = "Invalid ticket priority." });
        if (request.Subject is not null) { if (string.IsNullOrWhiteSpace(request.Subject)) return BadRequest(new { message = "Subject cannot be empty." }); ticket.Subject = request.Subject.Trim(); }
        if (request.Category is not null) ticket.Category = request.Category.Trim();
        if (request.Priority is not null) ticket.Priority = request.Priority;
        ticket.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(ticket);
    }

    [HttpPatch("tickets/{ticketId:guid}/status")]
    public async Task<IActionResult> SetTicketStatus(Guid ticketId, StatusRequest request)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null) return NotFound();
        var allowed = ticket.Status switch { "Open" => new[] { "InProgress", "Closed" }, "InProgress" => new[] { "WaitingCustomer", "Resolved", "Closed" }, "WaitingCustomer" => new[] { "InProgress", "Resolved", "Closed" }, "Resolved" => new[] { "Open", "Closed" }, _ => Array.Empty<string>() };
        if (!allowed.Contains(request.Status)) return Conflict(new { message = "Ticket status transition is not allowed.", currentStatus = ticket.Status });
        ticket.Status = request.Status; ticket.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(ticket);
    }

    [HttpPost("tickets/{ticketId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid ticketId, AssignmentRequest request)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null) return NotFound();
        if (request.StaffId == Guid.Empty) return BadRequest(new { message = "A staff ID is required." });
        ticket.AssignedStaffId = request.StaffId; ticket.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(ticket);
    }

    [HttpGet("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid ticketId)
    {
        if (!await db.Tickets.AnyAsync(x => x.Id == ticketId)) return NotFound();
        return Ok(await db.Messages.AsNoTracking().Where(x => x.TicketId == ticketId).OrderBy(x => x.CreatedAt).ToListAsync());
    }

    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid ticketId, MessageRequest request)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId);
        if (ticket is null) return NotFound();
        if (request.AuthorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Body)) return BadRequest(new { message = "Author and message body are required." });
        var message = new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, AuthorId = request.AuthorId, Body = request.Body.Trim(), IsInternal = request.IsInternal, CreatedAt = DateTime.UtcNow };
        db.Messages.Add(message); ticket.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Created($"/api/admin/support/tickets/{ticketId}/messages/{message.Id}", message);
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> ListComplaints([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.Complaints.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var count = await query.CountAsync(); return Ok(new { page, pageSize, totalItems = count, complaints = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("complaints/{complaintId:guid}")]
    public async Task<IActionResult> GetComplaint(Guid complaintId)
    {
        var item = await db.Complaints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == complaintId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint(ComplaintRequest request)
    {
        if (request.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description)) return BadRequest(new { message = "Customer, subject, and description are required." });
        var item = new Complaint { Id = Guid.NewGuid(), CustomerId = request.CustomerId, OrderId = request.OrderId, Subject = request.Subject.Trim(), Description = request.Description.Trim(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Complaints.Add(item); await db.SaveChangesAsync(); return Created($"/api/admin/support/complaints/{item.Id}", item);
    }

    [HttpPatch("complaints/{complaintId:guid}/status")]
    public async Task<IActionResult> SetComplaintStatus(Guid complaintId, StatusRequest request)
    {
        var item = await db.Complaints.SingleOrDefaultAsync(x => x.Id == complaintId);
        if (item is null) return NotFound();
        if (request.Status is not ("Open" or "Investigating" or "Resolved" or "Rejected")) return BadRequest(new { message = "Invalid complaint status." });
        item.Status = request.Status; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(item);
    }

    public sealed record CreateTicketRequest(Guid CustomerId, string Subject, string? Category = null, string Priority = "Normal", Guid AuthorId = default, string? InitialMessage = null);
    public sealed record UpdateTicketRequest(string? Subject = null, string? Category = null, string? Priority = null);
    public sealed record StatusRequest(string Status);
    public sealed record AssignmentRequest(Guid StaffId);
    public sealed record MessageRequest(Guid AuthorId, string Body, bool IsInternal = false);
    public sealed record ComplaintRequest(Guid CustomerId, string Subject, string Description, Guid? OrderId = null);
}
