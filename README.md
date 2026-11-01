# Growtopia Resmi Sunucu ENet Proxy Modu (C# / .NET)

Bu proje, resmi Growtopia istemcisi ile Ubisoft'un resmi Growtopia sunucuları arasına giren, paketleri çözümleyen, değiştiren ve özel oyun içi mod komutları ekleyen yüksek performanslı bir **ENet Proxy Modudur**.

---

## 🌟 Temel Özellikler

- **Resmi Sunucu Desteği & Sub-Server Yönlendirmesi:** Resmi Growtopia sunucularında oyuncu dünya değiştirdiğinde veya giriş yaptığında gönderilen `OnSendToServer` alt sunucu yönlendirmelerini otomatik olarak yakalar, istemciyi yerel proxy'ye bağlamaya devam ettirerek asıl alt sunucuya kesintisiz köprü kurar.
- **Dahili ENet Entegrasyonu:** macOS (Apple Silicon `arm64` ve Intel `x86_64`) ile Windows üzerinde yerel olarak derlenmiş dinamik kütüphaneler ile sıfır harici bağımlılıkla çalışır.
- **Oyun İçi Sohbet Komutları:** Oyun içi sohbette `/help`, `/ghost`, `/warp` gibi komutlar yazdığınızda, paketler sunucuya gitmeden yerel olarak yakalanır ve sonuç anında oyun ekranınıza (`OnConsoleMessage` / `OnTextOverlay`) yansıtılır.
- **Ghost Modu (`/ghost`):** Sunucuya giden hareket (`State`) paketlerini filtreler. Oyuncu kendi ekranında dünyada serbestçe gezinebilir, gizli alanları kontrol edebilir; ancak sunucu ve diğer oyuncular karakteri eski yerinde hareketsiz görür.
- **Dahili HTTP Yönlendirici (`server_data.php`):** İstemcinin resmi sunucu IP'sini sorguladığı HTTP isteğini yakalayarak yerel proxy'ye yönlendirir.
- **Detaylı Paket Analizörü (Packet Logger):** Gelen ve giden metin, VariantList ve Tank paketlerini filtreleme seçenekleriyle konsola renkli olarak döker.

---

## 📁 Proje Dizin Yapısı

```
GtMod/
├── Core/
│   ├── Logger.cs              # Renkli ve yapılandırılmış konsol çıktısı
│   └── ProxyConfig.cs         # Proxy yapılandırması (Portlar, filtreler, IP)
├── Protocol/
│   ├── NetMessageType.cs      # Growtopia paket tip tanımlamaları
│   ├── TankPacketType.cs      # TankPacket (GameUpdatePacket) tip sabitleri
│   ├── TankPacket.cs          # 56-baytlık ana oyun paketi ayrıştırıcısı
│   ├── VariantList.cs         # RPC / CallFunction VariantList kodlayıcı & çözücü
│   ├── TextPacket.cs          # 'action|log' gibi metin paketleri işleyicisi
│   └── PacketBuilder.cs       # Konsol mesajı, diyalog vb. paket fabrikası
├── Network/
│   ├── ProxyEngine.cs         # ENet çift yönlü ağ motoru ve paket yönlendirme
│   ├── HttpServer.cs          # server_data.php yönlendirme sunucusu
│   └── OfficialServerResolver.cs # Resmi Growtopia IP ve portunu dinamik çekici
├── Commands/
│   ├── ICommand.cs            # Komut arayüzü
│   ├── CommandManager.cs      # Oyun içi ve konsol komut yöneticisi
│   ├── HelpCommand.cs         # /help
│   ├── GhostCommand.cs        # /ghost
│   ├── WarpCommand.cs         # /warp <dünya>
│   ├── StatusCommand.cs       # /status
│   ├── LogCommand.cs          # /log [all/none/move]
│   ├── OverlayCommand.cs      # /overlay <mesaj>
│   ├── PosCommand.cs          # /pos
│   ├── DropCommand.cs         # /drop <id> <adet>
│   ├── TargetCommand.cs       # /target <ip> [port]
│   └── ResolveCommand.cs      # /resolve
├── Libraries/
│   ├── ENet.cs                # ENet C# sarmalayıcısı
│   ├── libenet.dylib          # macOS Universal ikilisi (arm64 + x86_64)
│   └── enet.dll               # Windows ikilisi
└── Program.cs                 # Ana giriş noktası ve terminal döngüsü
```

---

## 🚀 Çalıştırma ve Kurulum

### 1. Gereksinimler
- [.NET 9 veya .NET 10 SDK](https://dotnet.microsoft.com/download) (Sisteminizde zaten yüklü)

### 2. Derleme & Başlatma
Terminalde proje dizininde:

```bash
cd /Users/mertarslan/Desktop/GtMod
dotnet run
```

> **İpucu:** Eğer dahili HTTP sunucusunun doğrudan port 80 üzerinden `server_data.php` yanıtlamasını istiyorsanız, Unix/macOS işletim sistemlerinde port 80 için yönetici izni gerekebilir:
> ```bash
> sudo dotnet run
> ```

---

## 🎮 Oyunu Proxy'ye Bağlama Yöntemleri

Growtopia istemcisini proxy'nize bağlamak için en yaygın ve kararlı yöntem **hosts** dosyasını yönlendirmektir:

### macOS / Linux:
Terminalden `sudo nano /etc/hosts` dosyasını açın ve en alta şu satırları ekleyin:
```text
127.0.0.1 www.growtopia1.com
127.0.0.1 www.growtopia2.com
127.0.0.1 growtopia1.com
127.0.0.1 growtopia2.com
```
Kaydetmek için `Ctrl + O` ardından `Enter`, çıkmak için `Ctrl + X` tuşlarına basın.

### Windows:
`C:\Windows\System32\drivers\etc\hosts` dosyasını Yönetici olarak Not Defteri ile açıp aynı satırları ekleyin.

Ardından Growtopia'yı normal şekilde açıp giriş yapın. Tüm bağlantı doğrudan proxy konsolunuzda görünecektir!

---

## ⌨️ Oyun İçi Komutlar

Oyundayken normal sohbet kutusuna aşağıdaki komutları yazabilirsiniz:

| Komut | Açıklama | Örnek |
| :--- | :--- | :--- |
| `/help` | Tüm mod komutlarını ve açıklamalarını listeler. | `/help` |
| `/ghost` | Ghost modunu açar/kapatır. Hareketleriniz sunucuya iletilmez. | `/ghost on` veya `/ghost` |
| `/warp <dünya>` | Belirtilen dünyaya anında geçiş isteği gönderir. | `/warp START` |
| `/status` | Aktif dünya, NetID, piksel ve blok koordinatlarını gösterir. | `/status` |
| `/pos` | Karakterin tam blok ve piksel koordinatını verir. | `/pos` |
| `/overlay <metin>` | Ekranda büyük animasyonlu duyuru yazısı gösterir. | `/overlay Merhaba Growtopia!` |
| `/autoclave` | Seçilen cerrahi aleti stok bitene kadar otomatik dönüştürür ve diğer toolları yere atar. | `/ac anes`, `/ac 1262`, `/ac stop`, `/ac drop` |
| `/drop <id> [adet]` | Belirtilen eşyayı otomatik onaylayarak hızlıca yere bırakır. | `/drop 1266` (veya /drop 242 1) |
| `/log [seçenek]` | Paket loglarını yönetir (`all`, `none`, `move`, `packets`). | `/log move` |
| `/target <ip> [port]` | Hedef sunucu adresini manuel olarak değiştirir. | `/target 213.179.209.168 17203` |
| `/resolve` | Resmi Growtopia sunucularını yeniden sorgular. | `/resolve` |

> Aynı komutları doğrudan proxy'nin çalıştığı konsol penceresine de yazabilirsiniz!

---

## 🛠️ Yeni Mod Özelliği Eklemek

Yeni bir komut eklemek için `Commands/` klasörüne `ICommand` arayüzünü uygulayan yeni bir sınıf ekleyip `Program.cs` içine kaydetmeniz yeterlidir:

```csharp
public class MyFeatureCommand : ICommand
{
    public string Name => "myfeature";
    public string Description => "Özel özelliğim";
    public string Usage => "/myfeature";
    public string[] Aliases => new[] { "mf" };

    public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
    {
        engine.SendClientMessage("`2Özel özellik çalıştırıldı!`o");
    }
}
```
