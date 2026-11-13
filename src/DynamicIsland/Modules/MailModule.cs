using DynamicIsland.Localization;
using DynamicIsland.Services;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

namespace DynamicIsland.Modules;

public sealed record MailItem(string From, string Initial, string Subject, string TimeText, double UnreadDotOpacity);

/// <summary>
/// Reads the inbox over IMAP in read-only mode (messages are never marked as read).
/// Gmail: turn on 2-Step Verification → Google Account → App passwords → put the generated password in settings.json.
/// </summary>
public sealed class MailModule : IIslandModule
{
    private readonly MailSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(2));
    private bool _busy;

    public MailModule(MailSettings settings) => _settings = settings;

    public string Name => "Mail";
    public int Priority => 1;
    public bool IsActive => UnreadCount > 0;

    public bool IsConfigured => _settings.IsConfigured;
    public string Account => _settings.Username;
    public string WebUrl => _settings.WebUrl;
    public int? UnreadCount { get; private set; }
    public IReadOnlyList<MailItem> Messages { get; private set; } = Array.Empty<MailItem>();

    /// <summary>Translation key of the current status (translated in the view model so it follows the language).</summary>
    public string StatusKey { get; private set; } = "";
    public string StatusArgument { get; private set; } = "";

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (!IsConfigured)
        {
            SetStatus("MailStatusNotConnected");
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (_busy) return;
        _busy = true;

        SetStatus("MailStatusUpdating");
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            using var client = new ImapClient { Timeout = 15000 };
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(_settings.Username, _settings.AppPassword);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var unread = await inbox.SearchAsync(SearchQuery.NotSeen);
            UnreadCount = unread.Count;

            var list = new List<MailItem>();
            if (inbox.Count > 0)
            {
                int min = Math.Max(0, inbox.Count - 3);
                var request = new FetchRequest(MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate);
                var summaries = await inbox.FetchAsync(min, -1, request);

                foreach (var m in summaries.OrderByDescending(s => s.Index))
                {
                    var sender = m.Envelope?.From?.Mailboxes.FirstOrDefault();
                    var name = !string.IsNullOrWhiteSpace(sender?.Name) ? sender!.Name : sender?.Address;
                    if (string.IsNullOrWhiteSpace(name)) name = Loc.T("MailUnknownSender");

                    var subject = string.IsNullOrWhiteSpace(m.Envelope?.Subject) ? Loc.T("MailNoSubject") : m.Envelope!.Subject;
                    var date = (m.InternalDate ?? m.Envelope?.Date ?? DateTimeOffset.Now).LocalDateTime;
                    bool isUnread = m.Flags is MessageFlags flags && !flags.HasFlag(MessageFlags.Seen);

                    list.Add(new MailItem(name, name[..1].ToUpper(Loc.Instance.Culture), subject, FormatTime(date), isUnread ? 1 : 0));
                }
            }

            Messages = list;
            SetStatus("MailStatusUpdated", DateTime.Now.ToString("HH:mm"));
            await client.DisconnectAsync(true);
        }
        catch (AuthenticationException)
        {
            SetStatus("MailStatusAuthFailed");
        }
        catch (Exception ex)
        {
            SetStatus("MailStatusFailed", ex.Message);
        }
        finally
        {
            _busy = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetStatus(string key, string argument = "")
    {
        StatusKey = key;
        StatusArgument = argument;
    }

    private static string FormatTime(DateTime date) =>
        date.Date == DateTime.Today ? date.ToString("HH:mm") : date.ToString("d MMM", Loc.Instance.Culture);

    public void Dispose() => _timer.Dispose();
}
