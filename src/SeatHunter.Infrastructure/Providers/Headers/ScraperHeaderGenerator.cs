namespace SeatHunter.Infrastructure.Providers.Headers;

public static class ScraperHeaderGenerator
{
    private static readonly (string UserAgent, string Platform, string SecChUa)[] Fingerprints = new[]
    {
        (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
            "\"Windows\"",
            "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\""
        ),
        (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36",
            "\"Windows\"",
            "\"Chromium\";v=\"129\", \"Google Chrome\";v=\"129\", \"Not?A_Brand\";v=\"8\""
        ),
        (
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
            "\"macOS\"",
            "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\""
        )
    };

    public static void ApplyBrowserHeaders(HttpRequestMessage request)
    {
        var fp = Fingerprints[Random.Shared.Next(Fingerprints.Length)];

        request.Headers.Clear();
        request.Headers.Add("User-Agent", fp.UserAgent);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        request.Headers.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Site", "none");
        request.Headers.Add("Sec-Fetch-User", "?1");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Cache-Control", "max-age=0");
        request.Headers.Add("Sec-Ch-Ua", fp.SecChUa);
        request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
        request.Headers.Add("Sec-Ch-Ua-Platform", fp.Platform);
    }
}
