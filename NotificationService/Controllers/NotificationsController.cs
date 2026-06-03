using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using NotificationService.Models;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMongoCollection<Notification> _notifications;

    public NotificationsController(IMongoCollection<Notification> notifications)
    {
        _notifications = notifications;
    }

    /// <summary>
    /// Получить все уведомления (последние — первыми)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _notifications
            .Find(_ => true)
            .SortByDescending(n => n.ReceivedAt)
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Количество уведомлений
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var count = await _notifications.CountDocumentsAsync(_ => true);
        return Ok(new { count });
    }
}
