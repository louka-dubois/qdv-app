using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using MimeKit;
using QDVapp.Models;

namespace QDVapp.Services;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Lazy<X509Certificate2Collection> _trustedRoots;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _trustedRoots = new Lazy<X509Certificate2Collection>(LoadTrustedRoots);
    }

    public async Task SendEmailAsync(ApplicationUser user, string subject, string htmlMessage)
    {
        await SendEmailAsync(user.Email!, subject, htmlMessage);
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        await SendEmailAsync(email, "Confirmez votre courriel - QDVapp",
            $"""
            <h2>Bienvenue sur QDVapp!</h2>
            <p>Veuillez confirmer votre adresse courriel en cliquant sur le lien ci-dessous:</p>
            <p><a href="{confirmationLink}">Confirmer mon courriel</a></p>
            <p>Si vous n'avez pas créé de compte, vous pouvez ignorer ce courriel en toute sécurité.</p>
            """);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        await SendEmailAsync(email, "Réinitialisation du mot de passe - QDVapp",
            $"""
            <h2>Réinitialisation du mot de passe</h2>
            <p>Vous avez demandé une réinitialisation de mot de passe. Cliquez sur le lien ci-dessous pour définir un nouveau mot de passe:</p>
            <p><a href="{resetLink}">Réinitialiser mon mot de passe</a></p>
            <p>Si vous n'avez pas fait cette demande, vous pouvez ignorer ce courriel en toute sécurité.</p>
            """);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        await SendEmailAsync(email, "Code de réinitialisation du mot de passe - QDVapp",
            $"""
            <h2>Code de réinitialisation du mot de passe</h2>
            <p>Votre code de réinitialisation est:</p>
            <p><strong>{resetCode}</strong></p>
            <p>Si vous n'avez pas fait cette demande, vous pouvez ignorer ce courriel en toute sécurité.</p>
            """);
    }

    private async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var host = smtpSection["Host"] ?? throw new InvalidOperationException("SMTP Host is not configured.");
        var port = int.Parse(smtpSection["Port"] ?? "587");
        var username = smtpSection["Username"] ?? throw new InvalidOperationException("SMTP Username is not configured.");
        var password = smtpSection["Password"] ?? throw new InvalidOperationException("SMTP Password is not configured.");
        var fromEmail = smtpSection["FromEmail"] ?? username;
        var fromName = smtpSection["FromName"] ?? "QDVapp";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient
            {
                ServerCertificateValidationCallback = ValidateServerCertificate
            };
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Email sent to {Email} with subject '{Subject}'.", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}.", email);
            throw;
        }
    }

    /// <summary>
    /// Strictly validates the server certificate. Prefers the OS/system trust store, but falls
    /// back to app-embedded trusted roots (e.g. Google's GTS Root R4) so the app works on hosts
    /// whose OS is missing the latest root. It never blindly accepts arbitrary certificates.
    /// </summary>
    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
            return false;

        // 1) If the system trust store already validates the chain, accept it.
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        // 2) Otherwise, validate strictly against our embedded trusted roots.
        try
        {
            using var cert = new X509Certificate2(certificate);
            var rootCerts = _trustedRoots.Value;
            if (rootCerts.Count == 0)
                return false;

            var policy = new X509ChainPolicy
            {
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag
            };
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.CustomTrustStore.AddRange(rootCerts);
            policy.ExtraStore.AddRange(rootCerts);

            using var policyChain = new X509Chain();
            policyChain.ChainPolicy = policy;
            if (!policyChain.Build(cert))
            {
                _logger.LogWarning("SMTP certificate rejected by embedded roots. Errors: {Errors}",
                    string.Join("; ", policyChain.ChainStatus.Select(s => s.StatusInformation)));
                return false;
            }

            return policyChain.ChainStatus.Length == 0 ||
                   policyChain.ChainStatus.All(s => s.Status == X509ChainStatusFlags.NoError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP certificate validation failed with an exception.");
            return false;
        }
    }

    private X509Certificate2Collection LoadTrustedRoots()
    {
        var roots = new X509Certificate2Collection();
        var configured = _configuration.GetSection("Smtp")["TrustedRoots"];
        if (string.IsNullOrWhiteSpace(configured))
            return roots;

        // Resolve relative to the content root, falling back to the output directory (published app).
        var dirs = new[]
        {
            configured,
            Path.Combine(AppContext.BaseDirectory, configured)
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.pem", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var cert = new X509Certificate2(file);
                    roots.Add(cert);
                    _logger.LogInformation("Loaded trusted SMTP certificate: {Subject}", cert.Subject);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load trusted certificate from {Path}", file);
                }
            }
            if (roots.Count > 0)
                break;
        }

        return roots;
    }
}
