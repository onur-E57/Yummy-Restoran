# 🍽️ Yummy Restoran - Full-Stack Web Application

Modern, dinamik ve kullanıcı dostu bir restoran yönetim ve vitrin platformu. Bu proje, N-Tier (Çok Katmanlı) mimari prensiplerine bağlı kalınarak **ASP.NET Core Web API** ve **ASP.NET Core MVC** kullanılarak uçtan uca (Full-Stack) geliştirilmiştir.

## ✨ Öne Çıkan Özellikler

* **Dinamik Menü Yönetimi:** Ürünler, kategoriler ve şefler API üzerinden yönetilebilir.
* **Rezervasyon Sistemi:** Kullanıcıların kolayca masa ayırtabilmesi için entegre rezervasyon modülü.
* **Çok Katmanlı Mimari (N-Tier):** Veri erişimi (Data Access), iş mantığı (Business) ve sunum (Presentation) katmanlarının birbirinden bağımsız, temiz kod prensipleriyle (Clean Code) ayrılması.
* **RESTful Web API:** UI tarafını besleyen, ölçeklenebilir ve güvenli backend servisleri.
* **Responsive Tasarım:** Tüm mobil ve masaüstü cihazlarda kusursuz çalışan UI (Kullanıcı Arayüzü).

## 🛠️ Kullanılan Teknolojiler

**Backend (Sunucu Tarafı):**
* C# & .NET 6 / 8
* ASP.NET Core Web API
* Entity Framework Core (Code-First Approach)
* SQL Server & LINQ
* AutoMapper (DTO - Data Transfer Object)

**Frontend (İstemci Tarafı):**
* ASP.NET Core MVC
* HTML5, CSS3, JavaScript
* Bootstrap / UI Frameworks

## 🚀 Kurulum ve Çalıştırma

Projeyi kendi bilgisayarınızda çalıştırmak için aşağıdaki adımları izleyebilirsiniz:
1. **Repoyu Klonlayın:**
   ```bash
   git clone [https://github.com/onur-E57/Yummy-Restoran.git](https://github.com/onur-E57/Yummy-Restoran.git)

2. Veritabanını Hazırlayın:
SQL Server Management Studio'yu (SSMS) açın.
Proje dizininde UI katmanında bulunan Requirements klasörünün içerisindeki script.sql dosyasını SSMS içine sürükleyip F5 ile çalıştırın. Bu işlem, tabloları otomatik oluşturacak ve test verilerini (Mock Data) içeri aktaracaktır.

3. Bağlantı Ayarlarını Yapılandırın:
StajProje.WebApi projesi içindeki Context klasörünün içerisindeki ApiContext dosyasına gidin.
ConnectionStrings bölümünü kendi yerel SQL Server adresinize göre güncelleyin.

4. Projeyi Ayağa Kaldırın:
Visual Studio'da Solution'a sağ tıklayıp "Set Startup Projects" seçeneğine gidin.
Multiple startup projects (Birden fazla başlangıç projesi) seçeneğini işaretleyip hem WebApi hem de WebUI projelerinin aksiyonunu "Start" olarak ayarlayın.
Projeyi çalıştırın.

👨‍💻 Geliştirici
Onur Elmas
💼 LinkedIn: https://www.linkedin.com/in/onur-elmas-9b55a4293/
🌐 Portfolyo: onurelmas-portfolio.vercel.app
✉️ İletişim: onur.elmas04@gmail.com
