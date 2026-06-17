using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace SampleApp.Controllers;

// Sends a few sample emails through the Mailbox SMTP sink (localhost:2525) so
// the /_mailbox dashboard has something to show.
[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    [HttpPost("send-samples")]
    public IActionResult SendSamples()
    {
#pragma warning disable SYSLIB0014 // SmtpClient is fine for sending to a local dev sink
        using var smtp = new SmtpClient("localhost", 2525);

        var welcome = new MailMessage("noreply@shop.test", "maya@example.com")
        {
            Subject = "Welcome to the shop",
            IsBodyHtml = true,
            Body = """
                <div style="font-family:system-ui,sans-serif;max-width:520px;margin:auto;padding:24px">
                  <h1 style="color:#16a34a">Welcome, Maya</h1>
                  <p>Your account is ready. Here's a coupon for your first order:</p>
                  <p style="font-size:22px;font-weight:700;letter-spacing:2px">WELCOME10</p>
                  <a href="https://shop.test/start" style="display:inline-block;background:#16a34a;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Start shopping</a>
                </div>
                """,
        };
        smtp.Send(welcome);

        var receipt = new MailMessage("billing@shop.test", "tomer@example.com")
        {
            Subject = "Your order #4827 receipt",
            IsBodyHtml = true,
            Body = "<h2>Order #4827</h2><table><tr><td>Mechanical keyboard</td><td>$89.00</td></tr><tr><td>Webcam</td><td>$59.99</td></tr></table><p><b>Total: $148.99</b></p>",
        };
        var pdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake receipt body");
        receipt.Attachments.Add(new Attachment(new MemoryStream(pdf), "receipt-4827.pdf", "application/pdf"));
        smtp.Send(receipt);

        var reset = new MailMessage("noreply@shop.test", "priya@example.com")
        {
            Subject = "Reset your password",
            Body = "Someone asked to reset your password. If it wasn't you, ignore this. Link expires in 30 minutes.",
        };
        smtp.Send(reset);
#pragma warning restore SYSLIB0014

        return Ok(new { sent = 3 });
    }
}
