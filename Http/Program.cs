
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Http
{
    class Program
    {
        static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

            Console.WriteLine("=== Модуль HTTP: Поисковое приложение (Задания 1-4) ===");
            Console.Write("Введите текст для поиска(eng): ");
            string query = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(query)) return;

            Console.WriteLine("\nВыберите действие:");
            Console.WriteLine("1. Поиск текста (Bing)");
            Console.WriteLine("2. Поиск текста (Bing + DuckDuckGo одновременно)");
            Console.WriteLine("3. Поиск изображений (Bing)");
            Console.WriteLine("4. Поиск изображений (Bing + Yahoo одновременно)");
            Console.Write("Ваш выбор: ");
            string choice = Console.ReadLine();

            Console.WriteLine("\nВыполнение запроса...\n");
            string encodedQuery = Uri.EscapeDataString(query);

            switch (choice)
            {
                case "1":
                    await SearchTextAsync("Bing", $"https://www.bing.com/search?q={encodedQuery}");
                    break;
                case "2":
                    var t1 = SearchTextAsync("Bing", $"https://www.bing.com/search?q={encodedQuery}");

                    var t2 = SearchTextAsync("DuckDuckGo", $"https://html.duckduckgo.com/html/?q={encodedQuery}");
                    await Task.WhenAll(t1, t2);
                    break;
                case "3":
                    await SearchImagesAsync("Bing", $"https://www.bing.com/images/search?q={encodedQuery}");
                    break;
                case "4":
                    var img1 = SearchImagesAsync("Bing", $"https://www.bing.com/images/search?q={encodedQuery}");
                    var img2 = SearchImagesAsync("Flickr", $"https://www.flickr.com/search/?text={encodedQuery}");
                    await Task.WhenAll(img1, img2);
                    break;
            }

            Console.WriteLine("\nРабота завершена. Нажмите любую клавишу...");
            Console.ReadKey();
        }

        static async Task SearchTextAsync(string engineName, string url)
        {
            try
            {
                string html = await client.GetStringAsync(url);
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);

                HtmlNodeCollection nodes = null;

                if (engineName == "Bing")
                    nodes = document.DocumentNode.SelectNodes("//li[@class='b_algo']//h2/a");
                else if (engineName == "DuckDuckGo")
                    nodes = document.DocumentNode.SelectNodes("//a[@class='result__url']");

                Console.WriteLine($"--- Результаты текста: {engineName} ---");

                if (nodes != null)
                {
                    int count = 0;
                    foreach (var node in nodes)
                    {
                        if (count >= 5) break;

                        string title = HtmlEntity.DeEntitize(node.InnerText).Trim();
                        string link = node.GetAttributeValue("href", "");

                        if (engineName == "DuckDuckGo" && link.StartsWith("//duckduckgo.com/l/?uddg="))
                        {
                            link = Uri.UnescapeDataString(link.Replace("//duckduckgo.com/l/?uddg=", "").Split('&')[0]);
                        }

                        if (engineName == "Bing" && link.Contains("u=a1"))
                        {
                            var match = Regex.Match(link, @"u=a1([A-Za-z0-9_-]+)");
                            if (match.Success)
                            {
                                string base64Url = match.Groups[1].Value.Replace("-", "+").Replace("_", "/");
                                switch (base64Url.Length % 4)
                                {
                                    case 2: base64Url += "=="; break;
                                    case 3: base64Url += "="; break;
                                }
                                try { link = Encoding.UTF8.GetString(Convert.FromBase64String(base64Url)); }
                                catch { }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                        {
                            Console.WriteLine($"{count + 1}. {title}");
                            Console.WriteLine($"   Ссылка: {link}\n");
                            count++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Ничего не найдено (возможно, поисковик выдал капчу).\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{engineName}] Ошибка: {ex.Message}\n");
            }
        }

        static async Task SearchImagesAsync(string engineName, string url)
        {
            try
            {
                string html = await client.GetStringAsync(url);
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);

                HtmlNodeCollection imgNodes = null;

                if (engineName == "Bing")
                {
                    imgNodes = document.DocumentNode.SelectNodes("//img[contains(@class, 'mimg')]");
                }
                else if (engineName == "Flickr")
                {
                    imgNodes = document.DocumentNode.SelectNodes("//img[@loading='lazy']");
                }

                Console.WriteLine($"--- Результаты картинок: {engineName} ---");

                if (imgNodes != null)
                {
                    int count = 0;
                    foreach (var img in imgNodes)
                    {
                        if (count >= 5) break;

                        string src = img.GetAttributeValue("data-src", "");
                        if (string.IsNullOrEmpty(src)) src = img.GetAttributeValue("data-url", "");
                        if (string.IsNullOrEmpty(src)) src = img.GetAttributeValue("src", "");

                        // Восстанавливаем относительные ссылки (начинаются с //)
                        if (src.StartsWith("//"))
                        {
                            src = "https:" + src;
                        }

                        if (!string.IsNullOrWhiteSpace(src) && !src.StartsWith("data:image") && !src.EndsWith(".svg"))
                        {
                            Console.WriteLine($"{count + 1}. Ссылка: {src}");
                            count++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Изображения не найдены (возможно, изменилась верстка сайта).\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{engineName}] Ошибка: {ex.Message}\n");
            }
        }
    }
}