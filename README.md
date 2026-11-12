# Dynamic Island for Windows

iPhone'daki Dynamic Island'dan esinlenen, ekranın üst ortasında duran küçük bir "ada". Saati, hava durumunu ve çalan şarkıyı gösterir. Fareyle üstüne gelince genişler ve medya kontrollerini açar.

> 🎬 _Buraya demo GIF ekle_

## Özellikler
Kapalıyken ekranın üstünde küçük bir çentik; fareyle üstüne gelince cam görünümlü bir panele dönüşür. Panelin altındaki sekmelerle, fareyle sürükleyerek, tekerlekle ya da touchpad'de iki parmakla sayfalar arasında geçilir.

| Sayfa | Ne yapar |
|---|---|
| 🏠 **Ana Sayfa** | Hava, müzik, odak ve posta özet kartları |
| 🎵 **Müzik** | Kapak, başlık, ilerleme çubuğu, önceki / oynat / sonraki (Spotify, YouTube, Apple Music…) |
| ⏱ **Odak** | Cetvelle 1–60 dk seç, başlat/duraklat. Çalışırken kapalı adada geri sayım, bitince ada açılır |
| 🌤 **Hava** | Anlık durum, en yüksek/en düşük, 5 saatlik tahmin (Open-Meteo) |
| ✉️ **Posta** | Okunmamış sayısı ve son e-postalar (IMAP, salt okunur) |
| 🤖 **Claude Code** | Claude Code ile son çalışılan projeler; tıklayınca o klasörde terminal açıp `claude` başlatır |
| 💻 **VS Code** | VS Code'da son açılan klasörler; tek tıkla aç, yeni pencere, klasör seç |
| 📝 **Notlar** | Hızlı not defteri, yazarken otomatik kaydeder |
| 📅 **Takvim** | Ay görünümü ve yaklaşan etkinlikler (Google/Outlook iCal adresi) |

Ayrıca:
- 🎨 **Renk alan cam:** müzik çalarken albüm kapağının rengini alır; her sayfanın kendi rengi var
- 💧 Ekranın üst kenarına içe kıvrılan köşelerle bağlanan, elastik animasyonlu şekil
- 🎮 Tam ekran uygulamalarda otomatik gizlenir; Alt+Tab'da görünmez, odak çalmaz

## Gereksinimler
- Windows 10 (2004+) / Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Çalıştırma
```bash
cd src/DynamicIsland
dotnet run
```
Ya da `src/DynamicIsland/DynamicIsland.csproj` dosyasını Visual Studio 2022 veya Rider ile açabilirsin.

Kapatmak için adayı aç, sağ üstteki **⋯ → Çıkış**.

## Ayarlar
İlk çalıştırmada `%AppData%\DynamicIsland\settings.json` oluşturulur. Panelde sağ üstteki **⋯ → Ayarlar dosyasını aç** ile açabilirsin. Değişiklikten sonra uygulamayı yeniden başlat.

```jsonc
{
  "Weather":  { "Latitude": 41.20, "Longitude": 32.62, "LocationName": "Karabük" },
  "Mail": {
    "ImapHost": "imap.gmail.com",   // Outlook: outlook.office365.com
    "ImapPort": 993,
    "Username": "ornek@gmail.com",
    "AppPassword": "xxxx xxxx xxxx xxxx",  // Gmail: Google Hesabı → Güvenlik → Uygulama şifreleri
    "WebUrl": "https://mail.google.com"
  },
  "Calendar": {
    "IcsUrl": "https://calendar.google.com/calendar/ical/.../basic.ics",  // "iCal biçimindeki gizli adres"
    "WebUrl": "https://calendar.google.com"
  },
  "ClaudeCode": { "Projects": [] },              // otomatik bulunanlara ek klasörler
  "VsCode":     { "CodePath": null, "ExtraFolders": [] }
}
```

> ⚠️ Uygulama şifresi bu dosyada düz metin olarak durur. Normal Google şifreni değil, yalnızca bu iş için oluşturduğun uygulama şifresini yaz; istediğin zaman iptal edebilirsin.

Notlar `%AppData%\DynamicIsland\notes.json` dosyasında tutulur.

## Tek .exe olarak paketleme
```bash
dotnet publish src/DynamicIsland -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Mimari
```
src/DynamicIsland/
├── Modules/                 # Her özellik bağımsız bir modül (IIslandModule)
│   ├── ClockModule.cs
│   ├── MediaModule.cs       # Windows.Media.Control (GSMTC)
│   ├── WeatherModule.cs     # Open-Meteo
│   ├── FocusModule.cs       # Pomodoro zamanlayıcı
│   ├── MailModule.cs        # IMAP (MailKit)
│   ├── CalendarModule.cs    # iCal okuyucu
│   ├── ClaudeCodeModule.cs  # ~/.claude/projects oturumlarından son projeler
│   └── VsCodeModule.cs      # VS Code state.vscdb'den son klasörler (SQLite)
├── ViewModels/MainViewModel.cs
├── Views/
│   ├── IslandWindow.xaml    # Ada kabuğu: cam şekil, kapalı görünüm, üst satır, sekmeler
│   └── Pages/               # Her sayfa ayrı bir UserControl
├── Controls/
│   ├── IslandShape.cs       # Ekrana akan cam şekil (animasyonlu geometri + parıltı)
│   └── TickRuler.cs         # Odak süresi için cetvel
├── Services/                # Ayarlar, uygulama başlatıcı, notlar, renk çıkarma, Win32
└── Themes/Styles.xaml       # Ortak stiller
```
Yeni bir sayfa eklemek için: `Modules/` altına modül, `Views/Pages/` altına sayfa yaz; `IslandWindow.xaml`'daki şeride ve sekme çubuğuna ekle, `PageCount`'u artır.

## Yol haritası
- [ ] Sistem tepsisi (tray) ikonu ve ayarlar penceresi
- [ ] Ayarlar penceresi (şimdilik JSON dosyası)
- [ ] Windows ile birlikte başlatma
- [ ] Ses seviyesi / parlaklık değişince ada animasyonu
- [ ] Pil ve şarj durumu modülü
- [ ] Bildirim modülü
- [ ] Çoklu monitör desteği
- [ ] Gerçek arka plan bulanıklığı (Windows 11 acrylic)
- [ ] Pano (clipboard) geçmişi sayfası
- [ ] Tekrarlayan takvim etkinlikleri (RRULE)
- [ ] Uygulama şifresini Windows DPAPI ile şifreli saklama

## Lisans
MIT
