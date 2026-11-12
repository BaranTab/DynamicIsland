using DynamicIsland.Services;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;

namespace DynamicIsland.Modules;

public sealed record MailItem(string From, string Initial, string Subject, string TimeText, double UnreadDotOpacity);

/// <summary>
/// IMAP ile gelen kutusunu okur (salt okunur; e-postaları "okundu" işaretlemez).
/// Gmail için: 2 adımlı doğrulamayı aç → Google Hesabı → Uygulama şifreleri → oluşturduğun şifreyi settings.json'a yaz.
/// </summary>
public sealed class MailModule : IIslandModule
{
    private readonly MailSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(2));
    private bool _busy;

    public MailModule(MailSettings settings) => _settings = settings;

    public string Name => "Posta";
    public int Priority => 1;
    public bool IsActive => UnreadCount > 0;

    public bool IsConfigured => _settings.IsConfigured;
    public string Account => _settings.Username;
    public string WebUrl => _settings.WebUrl;
    public int? UnreadCount { get; private set; }
    public IReadOnlyList<MailItem> Messages { get; private set; } = Array.Empty<MailItem>();
    public string Status { get; private set; } = "";

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
            Status = "Hesap bağlı değil";
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (_busy) return;
        _busy = true;

        Status = "Güncelleniyor…";
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
                    if (string.IsNullOrWhiteSpace(name)) name = "Bilinmeyen";

                    var subject = string.IsNullOrWhiteSpace(m.Envelope?.Subject) ? "(Konu yok)" : m.Envelope!.Subject;
                    var date = (m.InternalDate ?? m.Envelope?.Date ?? DateTimeOffset.Now).LocalDateTime;
                    bool isUnread = m.Flags is MessageFlags flags && !flags.HasFlag(MessageFlags.Seen);

                    list.Add(new MailItem(name, name[..1].ToUpper(Format.Tr), subject, FormatTime(date), isUnread ? 1 : 0));
                }
            }

            Messages = list;
            Status = $"Güncellendi {DateTime.Now:HH:mm}";
            await client.DisconnectAsync(true);
        }
        catch (AuthenticationException)
        {
            Status = "Giriş başarısız: kullanıcı adını ve uygulama şifresini kontrol et";
        }
        catch (Exception ex)
        {
            Status = "Bağlanamadı: " + ex.Message;
        }
        finally
        {
            _busy = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatTime(DateTime date) =>
        date.Date == DateTime.Today ? date.ToString("HH:mm") : date.ToString("d MMM", Format.Tr);

    public void Dispose() => _timer.Dispose();
}
