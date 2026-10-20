namespace Gamefilled.Application.Email;

/// <summary>
/// Transport only. Account token generation and link security belong to Identity.
/// </summary>
public interface ITransactionalEmailSender
{
    Task SendAsync(
        string recipient,
        string subject,
        string text,
        string? html = null,
        CancellationToken cancellationToken = default);
}
