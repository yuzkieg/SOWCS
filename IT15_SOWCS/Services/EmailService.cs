using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;

namespace IT15_SOWCS.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendInviteEmailAsync(string toEmail, string recipientName, string inviterName, string inviterEmail, string joinLink)
        {
            var safeRecipientName = HtmlEncoder.Default.Encode(recipientName);
            var safeInviterName = HtmlEncoder.Default.Encode(inviterName);
            var safeInviterEmail = HtmlEncoder.Default.Encode(inviterEmail);
            var safeJoinLink = HtmlEncoder.Default.Encode(joinLink);

            var body = $"""
                <div style="font-family:Arial,sans-serif;background:#f5f7fb;padding:24px;">
                    <div style="max-width:620px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;">
                        <div style="background:#eef2f7;padding:36px 28px;text-align:center;font-size:52px;font-weight:500;color:#111827;">
                            Ready when you are
                        </div>
                        <div style="padding:28px;">
                            <p style="margin:0 0 18px 0;font-size:18px;color:#111827;">Hi {safeRecipientName},</p>
                            <p style="margin:0 0 20px 0;font-size:16px;color:#111827;">
                                <strong>{safeInviterName}</strong> ({safeInviterEmail}) has invited you to join Syncora.<br/>
                                We're excited to have you on board!
                            </p>
                            <div style="background:#f3f4f6;border:1px solid #e5e7eb;border-radius:8px;padding:22px;text-align:center;">
                                <h3 style="margin:0 0 8px 0;color:#111827;">About Syncora</h3>
                                <p style="margin:0 0 20px 0;color:#374151;">Manage projects, tasks, documents, and approvals in one workspace.</p>
                                <hr style="border:none;border-top:1px solid #d1d5db;margin:18px 0;"/>
                                <p style="margin:0 0 18px 0;color:#111827;">Ready to jump in? Click below to accept your invitation.</p>
                                <a href="{safeJoinLink}" style="display:inline-block;padding:12px 22px;background:#000000;color:#ffffff;text-decoration:none;border-radius:999px;font-weight:700;">
                                    Join Syncora now
                                </a>
                            </div>
                            <p style="margin:24px 0 0 0;color:#374151;">If you have any questions, feel free to reach out to our support team.</p>
                        </div>
                    </div>
                </div>
                """;

            return await SendAsync(toEmail, "You are invited to join Syncora", body);
        }

        public async Task<bool> SendVerificationCodeEmailAsync(string toEmail, string recipientName, string code)
        {
            var safeRecipientName = HtmlEncoder.Default.Encode(recipientName);
            var safeCode = HtmlEncoder.Default.Encode(code);

            var body = $"""
                <div style="font-family:Arial,sans-serif;background:#f5f7fb;padding:24px;">
                    <div style="max-width:620px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;">
                        <div style="background:#eef2f7;padding:36px 28px;text-align:center;font-size:52px;font-weight:500;color:#111827;">
                            Verify your email
                        </div>
                        <div style="padding:28px;">
                            <p style="margin:0 0 18px 0;font-size:18px;color:#111827;">Hey {safeRecipientName},</p>
                            <p style="margin:0 0 18px 0;color:#111827;">Please verify your email address to continue your password change request.</p>
                            <div style="background:#f3f4f6;border:1px solid #e5e7eb;border-radius:8px;padding:20px;text-align:center;margin:20px 0;">
                                <div style="color:#374151;margin-bottom:10px;">Your verification code</div>
                                <div style="font-size:48px;letter-spacing:10px;color:#f97316;font-weight:700;">{safeCode}</div>
                            </div>
                            <p style="margin:0;color:#111827;">This code will expire in <strong>10 minutes</strong>.</p>
                            <p style="margin-top:24px;color:#111827;">See you there,<br/>The Syncora team</p>
                        </div>
                    </div>
                </div>
                """;

            return await SendAsync(toEmail, "Syncora verification code", body);
        }

        public async Task<bool> SendResetPasswordEmailAsync(string toEmail, string recipientName, string resetLink)
        {
            var safeRecipientName = HtmlEncoder.Default.Encode(recipientName);
            var safeResetLink = HtmlEncoder.Default.Encode(resetLink);

            var body = $"""
                <div style="font-family:Arial,sans-serif;background:#f5f7fb;padding:24px;">
                    <div style="max-width:620px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;">
                        <div style="background:#eef2f7;padding:36px 28px;text-align:center;font-size:52px;font-weight:500;color:#111827;">
                            Reset your password
                        </div>
                        <div style="padding:28px;">
                            <p style="margin:0 0 18px 0;font-size:18px;color:#111827;">Hey {safeRecipientName},</p>
                            <p style="margin:0 0 18px 0;color:#111827;">We received a request to reset your password. If that was you, click the button below to choose a new one.</p>
                            <div style="background:#f3f4f6;border:1px solid #e5e7eb;border-radius:8px;padding:24px;text-align:center;margin:20px 0;">
                                <a href="{safeResetLink}" style="display:inline-block;padding:12px 22px;background:#000000;color:#ffffff;text-decoration:none;border-radius:999px;font-weight:700;">
                                    Reset password
                                </a>
                                <p style="margin:16px 0 0 0;color:#374151;font-size:13px;">If the button doesn't work, copy and paste this link into your browser:</p>
                                <p style="margin:8px 0 0 0;color:#1d4ed8;word-break:break-all;font-size:12px;">{safeResetLink}</p>
                            </div>
                            <p style="margin:0;color:#111827;">For your security, this link will expire in <strong>1 hour</strong>.</p>
                            <p style="margin:16px 0 0 0;color:#6b7280;font-size:13px;">If you didn't request this, you can ignore this email.</p>
                            <p style="margin-top:24px;color:#111827;">Stay secure,<br/>The Syncora Team</p>
                        </div>
                    </div>
                </div>
                """;

            return await SendAsync(toEmail, "Reset your Syncora password", body);
        }

        public async Task<bool> SendAccountReactivationRequestAsync(string toEmail, string requesterEmail, string message)
        {
            var safeRequesterEmail = HtmlEncoder.Default.Encode(requesterEmail);
            var safeMessage = HtmlEncoder.Default.Encode(message);

            var body = $"""
                <div style="font-family:Arial,sans-serif;background:#f5f7fb;padding:24px;">
                    <div style="max-width:620px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;">
                        <div style="background:#eef2f7;padding:28px;text-align:center;font-size:36px;font-weight:600;color:#111827;">
                            Account Reactivation Request
                        </div>
                        <div style="padding:24px;">
                            <p style="margin:0 0 12px 0;color:#111827;"><strong>Requester:</strong> {safeRequesterEmail}</p>
                            <p style="margin:0 0 8px 0;color:#111827;"><strong>Message:</strong></p>
                            <div style="border:1px solid #e5e7eb;border-radius:8px;background:#f8fafc;padding:14px;color:#334155;">
                                {safeMessage}
                            </div>
                        </div>
                    </div>
                </div>
                """;

            return await SendAsync(toEmail, "Syncora account reactivation request", body);
        }

        private async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPortRaw = _configuration["EmailSettings:SmtpPort"];
            var smtpUser = _configuration["EmailSettings:Username"];
            var smtpPassword = _configuration["EmailSettings:Password"];
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUser;
            var fromName = _configuration["EmailSettings:FromName"] ?? "Syncora";
            var enableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var parsedSsl) ? parsedSsl : true;

            if (string.IsNullOrWhiteSpace(toEmail) ||
                string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpPortRaw) ||
                string.IsNullOrWhiteSpace(smtpUser) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(fromEmail) ||
                !int.TryParse(smtpPortRaw, out var smtpPort))
            {
                return false;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl = enableSsl
            };

            try
            {
                await client.SendMailAsync(message);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
