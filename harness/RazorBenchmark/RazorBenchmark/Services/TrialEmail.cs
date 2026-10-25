using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RazorBenchmark.Services;

/// <summary>
/// One-click trial-request email workflow (public boundary only).
/// Builds a correctly percent-encoded mailto: URI from the official template
/// and opens it through the OS-registered email handler. Never sends email
/// itself, never needs SMTP credentials, API keys, or private infrastructure.
/// URI construction is pure and unit-testable; process launching is isolated
/// so tests run in CI without opening an email client.
/// </summary>
public static class TrialEmail
{
    public const string Recipient = "razor@mobisec.cloud";
    public const string Subject = "RazorZip 14-Day Enterprise Trial Request";

    /// <summary>Official template body. Single source of truth mirrored from
    /// docs/trial/trial-request-template.txt (embedded to avoid file-lookup
    /// failures in installed layouts; both copies are byte-compared in tests).</summary>
    public static string TemplateBody =>
        "Hello MobiSec Team,\n" +
        "\n" +
        "\n" +
        "I would like to request a 14-day RazorZip Enterprise trial.\n" +
        "\n" +
        "\n" +
        "--- BEGIN TRIAL REQUEST ---\n" +
        "Email Address: [Your Email Address]\n" +
        "Given Name: [Your First Name]\n" +
        "Last Name: [Your Last Name]\n" +
        "Cell Number: [Your Phone Number]\n" +
        "Business Name: [Company Name or write \"PRIVATE\"]\n" +
        "Heard About From: [e.g., GitHub, Hacker News, Colleague]\n" +
        "Country: [Your Country]\n" +
        "Town/City: [Your City]\n" +
        "--- END TRIAL REQUEST ---\n" +
        "\n" +
        "\n" +
        "Please let me know the next steps for obtaining and activating my trial license.\n" +
        "\n" +
        "\n" +
        "Kind regards,\n" +
        "[Your Name]\n";

    /// <summary>RFC 6068 mailto: with UTF-8 percent-encoding (spaces, CRLF,
    /// brackets, unicode). Pure function — safe under test.</summary>
    public static string BuildMailtoUri(string? body = null)
        => "mailto:" + Uri.EscapeDataString(Recipient)
         + "?subject=" + Uri.EscapeDataString(Subject)
         + "&body=" + Uri.EscapeDataString(body ?? TemplateBody);

    /// <summary>Maximum safe length guard: some handlers truncate long URIs.
    /// Callers must use the copy/print fallback instead of a truncated send.</summary>
    public const int MaxUriLength = 2000;

    public static bool IsWithinSafeLength(string mailto)
        => mailto.Length <= MaxUriLength;

    /// <summary>Launch result — opening the client is NOT proof the user sent
    /// anything; the app cannot know whether the message was completed.</summary>
    public enum LaunchOutcome { Opened, NoHandler, Failed }

    /// <summary>
    /// Opens the mailto: URI via the OS-registered handler. Returns Opened only
    /// when the OS accepted the launch. Throws nothing; maps everything else to
    /// NoHandler/Failed with a message safe to display.
    /// </summary>
    public static (LaunchOutcome Outcome, string Message) TryOpenEmailClient(string mailto)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true,
                }
            };
            if (!proc.Start())
                return (LaunchOutcome.Failed, "The operating system declined to open the email application.");
            return (LaunchOutcome.Opened,
                "Your email application should now show a prefilled trial request. Review it and press Send yourself — nothing has been sent automatically.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                    || ex is PlatformNotSupportedException
                                    || ex is InvalidOperationException
                                    || ex is ObjectDisposedException)
        {
            if (IsHeadless())
                return (LaunchOutcome.NoHandler,
                    "No graphical email handler is available in this session (headless/SSH). Use the copy-or-print fallback below to submit the request manually.");
            return (LaunchOutcome.NoHandler,
                "No email application is registered to handle email links. Use the copy-or-print fallback below.");
        }
        catch (Exception ex)
        {
            return (LaunchOutcome.Failed,
                "Could not open the email application (" + ex.GetType().Name + "). Use the copy-or-print fallback below.");
        }
    }

    public static bool IsHeadless()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false; // Windows sessions used here always have shell association APIs.
        return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MIR_SOCKET"));
    }

    /// <summary>Manual-submission text for headless/no-handler sessions.</summary>
    public static string ManualInstructions() =>
        "To request a 14-day RazorZip Enterprise trial, send an email yourself:\n" +
        "  To:      " + Recipient + "\n" +
        "  Subject: " + Subject + "\n" +
        "Paste the template below into the message body, fill in the bracketed fields, and send.\n" +
        "Nothing has been sent automatically and no account or password is required.\n" +
        "---- template begins ----\n" + TemplateBody + "---- template ends ----\n";
}
