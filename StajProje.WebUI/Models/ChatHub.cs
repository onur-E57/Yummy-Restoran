using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using StajProje.WebUI.Dtos.ReservationDtos;

namespace StajProje.WebUI.Models
{
    public class ChatHub : Hub
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly Dictionary<string, List<GeminiContent>> _history = new();

        public ChatHub(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _apiKey = _configuration["GeminiConfig:ApiKey"];
            _httpClientFactory = httpClientFactory;
        }

        private const string AdminSystemInstruction = @"Sen Yummy Restoran'ın 'Yönetici ve Sistem Asistanısın'. Görevin yöneticiye yardım etmektir.";
        private const string CustomerSystemInstruction = @"Sen Yummy Restoran'ın canlı destek asistanısın. Müşterilere menü ve rezervasyon konusunda yardımcı ol. 

KESİN KURAL: Sana aşağıda verilecek olan 'VERİTABANINDAKİ GÜNCEL VE ONAYLI MENÜ' listesi dışındakileri ASLA söyleme, dışarıdan asla ürün/fiyat uydurma! Sadece veritabanında olanları söyle.

EĞER MÜŞTERİ REZERVASYON YAPMAK İSTERSE: Ad Soyad, E-posta (yoksa 'belirtilmedi@gmail.com'), Telefon Numarası, Tarih (DD-MM-YYYY formatında), Saat (HH:MM) ve Kişi Sayısı bilgilerini iste.

REZERVASYON ONAY AŞAMASI (ÇOK ÖNEMLİ):
Tüm bilgileri topladığında HEMEN işlemi tamamlama! Önce müşteriye bilgilerin bir özetini sun ve açıkça 'Bu bilgileri onaylıyor musunuz?' diye sor. Kullanıcı açıkça 'Evet', 'Onaylıyorum' veya 'Tamamdır' gibi bir onay vermeden ASLA kaydetme komutu gönderme. Müşteri değişiklik yapmak isterse bilgileri güncelle ve tekrar onay iste.

MÜŞTERİ KESİN ONAY VERDİKTEN SONRA:
Sadece ve sadece müşteri onay verdikten sonra, yanıtının en sonuna KESİNLİKLE şu formatı ekle: ||SAVE:AdSoyad|Eposta|Telefon|Tarih|Saat|KisiSayisi|| (Örn: ||SAVE:Onur Elmas|onur@gmail.com|05554443322|20-09-2026|20:00|2||) ve ardından rezervasyonun alındığına dair güzel bir onay mesajı yaz.";

        public override Task OnConnectedAsync()
        {
            _history[Context.ConnectionId] = new List<GeminiContent>();
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _history.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        // --- 2. RAG MİMARİSİ: Veritabanından Kategorili Menüyü Çeken Metot ---
        private async Task<string> GetMenuDataFromDatabaseAsync(string role)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // 1. Kategorileri Çekiyoruz
                var catResponse = await client.GetAsync("https://localhost:7143/api/Categories");
                var categories = new List<CategoryDto>();
                if (catResponse.IsSuccessStatusCode)
                {
                    var catJson = await catResponse.Content.ReadAsStringAsync();
                    categories = JsonSerializer.Deserialize<List<CategoryDto>>(catJson, options) ?? new List<CategoryDto>();
                }

                // 2. Ürünleri Çekiyoruz
                var prodResponse = await client.GetAsync("https://localhost:7143/api/Products");

                if (prodResponse.IsSuccessStatusCode)
                {
                    var prodJson = await prodResponse.Content.ReadAsStringAsync();
                    var products = JsonSerializer.Deserialize<List<ProductDto>>(prodJson, options);

                    if (products != null && products.Any())
                    {
                        var menuBuilder = new StringBuilder();
                        menuBuilder.AppendLine("İŞTE RESTORANIMIZIN GÜNCEL MENÜSÜ, KATEGORİLERİ VE İÇERİKLERİ:");

                        // 3. Ürünleri CategoryId'ye göre grupluyoruz
                        var groupedProducts = products.GroupBy(p => p.CategoryId);

                        foreach (var group in groupedProducts)
                        {
                            var categoryName = categories.FirstOrDefault(c => c.CategoryId == group.Key)?.CategoryName ?? "Diğer Lezzetlerimiz";
                            menuBuilder.AppendLine($"\n[{categoryName.ToUpper()}] KATEGORİSİ:");

                            foreach (var item in group)
                            {
                                menuBuilder.AppendLine($"- {item.ProductName} (İçindekiler: {item.ProductDescription}) : {item.ProductPrice} TL");
                            }
                        }
                        
                        // 4. Role Göre Kuralları Ekliyoruz
                        if (role == "admin")
                        {
                            menuBuilder.AppendLine("\nSİSTEM BİLGİSİ: Restoranın güncel menü veritabanı ve içerikleri yukarıdaki gibidir. Yönetici senden menüyle ilgili bir kampanya veya mail taslağı hazırlamanı isterse bu gerçek verileri kullan.");
                        }
                        else // customer (müşteri)
                        {
                            menuBuilder.AppendLine("\nÖNEMLİ KURAL: Müşteriye ürün önerirken SADECE YUKARIDAKİ GERÇEK MENÜYÜ kullan. Müşteri içerik veya alerjen sorarsa parantez içindeki 'İçindekiler' bilgisini baz al. Olmayan bir ürünü satmaya çalışma.");
                        }

                        return menuBuilder.ToString();
                    }
                }
            }
            catch
            {
                // API'ye ulaşılamazsa sessizce hatayı yut, hata patlatma
            }

            return "Şu an güncel menü veritabanından çekilemedi.";
        }

        public async Task SendMessage(string userMessage, string role = "customer")
        {
            await Clients.Caller.SendAsync("ReceiveUserEcho", userMessage);
            var history = _history[Context.ConnectionId];

            history.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart> { new GeminiPart { Text = userMessage } }
            });

            await StreamGemini(history, role, Context.ConnectionAborted);
        }

        public async Task StreamGemini(List<GeminiContent> history, string role, CancellationToken cancellationToken)
        {
            try
            {
                string baseInstruction = role == "admin" ? AdminSystemInstruction : CustomerSystemInstruction;

                // Müşteri asistanına özel dinamik veri formatı kuralını ekliyoruz
                if (role == "customer")
                {
                    baseInstruction += "\n\nKRİTİK KURAL: Müşteri Ad Soyad, E-posta, Telefon Numarası, Tarih (dd-MM-yyyy), Saat ve Kişi Sayısı bilgilerinin HEPSİNİ verdiğinde, yanıtının en sonuna KESİNLİKLE şu gizli formatı ekle: ||SAVE:AdSoyad|Eposta|Telefon|Tarih|Saat|KisiSayisi|| ve ardından nazik bir onay mesajı yaz.";
                }

                string menuContext = await GetMenuDataFromDatabaseAsync(role);
                string currentRealDate = DateTime.Now.ToString("dd-MM-yyyy");
                string dynamicSystemInstruction = $"{baseInstruction}\n\n" +
                                  $"GÜNCEL GERÇEK TARİH: {currentRealDate} (Bugünden önceki veya 2023 gibi geçmiş yılları ASLA kullanma, rezervasyonları veya tarihleri her zaman 2026 ve sonrasına ayarla!)\n\n" +
                                  $"{menuContext}";

                var client = _httpClientFactory.CreateClient();
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:streamGenerateContent?alt=sse&key={_apiKey}";

                var payload = new
                {
                    system_instruction = new { parts = new { text = dynamicSystemInstruction } },
                    contents = history,
                    generationConfig = new { temperature = 0.3 }
                };

                var serializeOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(JsonSerializer.Serialize(payload, serializeOptions), Encoding.UTF8, "application/json");

                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);
                var sb = new StringBuilder();

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                    var data = line.Substring(6).Trim();
                    try
                    {
                        var chunk = JsonSerializer.Deserialize<GeminiStreamChunk>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var part = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault();

                        if (part != null && !string.IsNullOrEmpty(part.Text))
                        {
                            sb.Append(part.Text);
                        }
                    }
                    catch { }
                }

                string fullResponse = sb.ToString();

                if (fullResponse.Contains("||SAVE:"))
                {
                    try
                    {
                        int startIndex = fullResponse.IndexOf("||SAVE:");
                        int endIndex = fullResponse.IndexOf("||", startIndex + 7);
                        if (startIndex != -1 && endIndex != -1)
                        {
                            string saveBlock = fullResponse.Substring(startIndex, endIndex - startIndex + 2);
                            string dataContent = saveBlock.Replace("||SAVE:", "").Replace("||", "");
                            string[] p = dataContent.Split('|');

                            if (p.Length >= 6)
                            {
                                var newReservation = new CreateReservationDto
                                {
                                    NameSurname = p[0].Trim(),
                                    Email = p[1].Trim(),
                                    PhoneNumber = string.IsNullOrEmpty(p[2].Trim()) ? "05000000000" : p[2].Trim(),
                                    ReservationDate = DateTime.Parse(p[3].Trim()),
                                    ReservationTime = p[4].Trim(),
                                    CountofPeople = int.Parse(p[5].Trim()),
                                    Message = "Yapay Zeka Canlı Destek Üzerinden Alındı",
                                    ReservationStatus = "Beklemede"
                                };

                                var apiReqContent = new StringContent(JsonSerializer.Serialize(newReservation), Encoding.UTF8, "application/json");
                                await client.PostAsync("https://localhost:7143/api/Reservations", apiReqContent);
                            }

                            // Gizli etiketi metinden temizleyelim ki kullanıcı görmesin
                            fullResponse = fullResponse.Replace(saveBlock, "").Trim();
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(fullResponse))
                {
                    history.Add(new GeminiContent { Role = "model", Parts = new List<GeminiPart> { new GeminiPart { Text = fullResponse } } });
                    await Clients.Caller.SendAsync("ReceiveToken", fullResponse, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveToken", $"\n[Sistem Hatası]: {ex.Message}", cancellationToken);
            }
            finally
            {
                await Clients.Caller.SendAsync("CompleteMessage", "", cancellationToken);
            }
        }

        // --- JSON DESERIALIZATION MODELLERİ (YENİ EKLENENLER) ---
        public sealed class GeminiStreamChunk { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
        public sealed class Candidate { [JsonPropertyName("content")] public GeminiContent? Content { get; set; } }
        public sealed class GeminiContent { [JsonPropertyName("role")] public string? Role { get; set; } [JsonPropertyName("parts")] public List<GeminiPart>? Parts { get; set; } }

        public sealed class GeminiPart
        {
            [JsonPropertyName("text")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Text { get; set; }
            [JsonPropertyName("functionCall")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FunctionCall? FunctionCall { get; set; }
            [JsonPropertyName("functionResponse")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FunctionResponse? FunctionResponse { get; set; }
        }

        public sealed class FunctionCall
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("args")] public JsonElement? Args { get; set; }
        }

        public sealed class FunctionResponse
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("response")] public object? Response { get; set; }
        }

        // --- RAG İÇİN GÜNCEL DTO'LAR ---
        public class ProductDto
        {
            public string ProductName { get; set; }
            public string ProductDescription { get; set; } // İçindekiler kısmı eklendi
            public decimal ProductPrice { get; set; } // Sende kolon adı ProductPrice
            public int CategoryId { get; set; }
        }

        public class CategoryDto
        {
            public int CategoryId { get; set; }
            public string CategoryName { get; set; } // Kategori API'ndeki isim kolonuna göre
        }
    }
}