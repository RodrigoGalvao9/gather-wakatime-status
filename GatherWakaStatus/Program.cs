using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static readonly HttpClient http = new HttpClient();

    static async Task<int> Main()
    {
        var wakaKey = Environment.GetEnvironmentVariable("WAKATIME_API_KEY");
        var gatherSecret = Environment.GetEnvironmentVariable("GATHER_WEBHOOK_SECRET");
        var gatherUrl = Environment.GetEnvironmentVariable("GATHER_WEBHOOK_URL")
            ?? "https://api.v2.gather.town/api/v2/hooks/spaces/9ed2542d-ef79-46ab-abae-8dc91849b3f8/objects/37c1b1e5-cfea-4029-bdf0-5c01472c4132";

        if (string.IsNullOrWhiteSpace(wakaKey))
        {
            Console.Error.WriteLine("WAKATIME_API_KEY não definida.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(gatherSecret) || !gatherSecret.StartsWith("whsec_"))
        {
            Console.Error.WriteLine("GATHER_WEBHOOK_SECRET ausente ou inválida (precisa começar com whsec_).");
            return 1;
        }

        string state;
        string activityText;

        try
        {
            var (lastHeartbeat, todayText) = await GetWakaTimeStatusAsync(wakaKey);
            var idleSeconds = (DateTimeOffset.UtcNow - lastHeartbeat).TotalSeconds;

            state = idleSeconds switch
            {
                <= 120 => "working",   // heartbeat nos últimos 2 min
                <= 1800 => "on",       // ativo hoje, mas não codando agora
                _ => "off"             // sem heartbeat recente
            };

            activityText = string.IsNullOrWhiteSpace(todayText)
                ? "Sem atividade registrada hoje"
                : $"{todayText} hoje";
        }
        catch (Exception ex)
        {
            // Falha na consulta ao WakaTime não deve derrubar o workflow —
            // sinaliza "question" no Gather e segue.
            Console.Error.WriteLine($"Falha ao consultar WakaTime: {ex.Message}");
            state = "question";
            activityText = "Não foi possível consultar o WakaTime";
        }

        await SendGatherEventAsync(gatherUrl, gatherSecret, "status.set", new { state });

        await SendGatherEventAsync(gatherUrl, gatherSecret, "activity.add", new
        {
            id = "wakatime-today",
            text = activityText,
            url = "https://wakatime.com/dashboard"
        });

        Console.WriteLine($"OK — status={state}");
        return 0;
    }

    static async Task<(DateTimeOffset lastHeartbeat, string todayText)> GetWakaTimeStatusAsync(string apiKey)
    {
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":"));

        // Total/texto do dia (ex: "2 hrs 34 mins")
        using var statusReq = new HttpRequestMessage(HttpMethod.Get,
            "https://wakatime.com/api/v1/users/current/status_bar/today");
        statusReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        using var statusResp = await http.SendAsync(statusReq);
        statusResp.EnsureSuccessStatusCode();
        using var statusJson = JsonDocument.Parse(await statusResp.Content.ReadAsStringAsync());
        var todayText = statusJson.RootElement
            .GetProperty("data")
            .GetProperty("grand_total")
            .GetProperty("text")
            .GetString() ?? "";

        // Heartbeats de hoje, para achar o mais recente
        using var hbReq = new HttpRequestMessage(HttpMethod.Get,
            "https://wakatime.com/api/v1/users/current/heartbeats?date=today");
        hbReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        using var hbResp = await http.SendAsync(hbReq);
        hbResp.EnsureSuccessStatusCode();
        using var hbJson = JsonDocument.Parse(await hbResp.Content.ReadAsStringAsync());

        double maxTime = 0;
        foreach (var hb in hbJson.RootElement.GetProperty("data").EnumerateArray())
        {
            var t = hb.GetProperty("time").GetDouble();
            if (t > maxTime) maxTime = t;
        }

        var lastHeartbeat = maxTime > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)(maxTime * 1000))
            : DateTimeOffset.UnixEpoch; // nenhum heartbeat hoje -> cai em "off"

        return (lastHeartbeat, todayText);
    }

    /// <summary>
    /// Assina e envia um evento para o Gather Smart Object, seguindo o esquema
    /// Standard Webhooks v1 (webhook-id / webhook-timestamp / webhook-signature).
    /// Implementação manual porque não há SDK oficial em C#/.NET.
    /// </summary>
    static async Task SendGatherEventAsync(string url, string secret, string type, object data)
    {
        var body = new
        {
            type,
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            data
        };

        // Serializa UMA vez e assina exatamente esses bytes — não reserializar depois.
        var rawBody = JsonSerializer.Serialize(body);
        var webhookId = Guid.NewGuid().ToString();
        var webhookTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var signedContent = $"{webhookId}.{webhookTimestamp}.{rawBody}";
        var keyBytes = Convert.FromBase64String(secret.Substring("whsec_".Length));

        using var hmac = new HMACSHA256(keyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = $"v1,{Convert.ToBase64String(signatureBytes)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        req.Headers.Add("webhook-id", webhookId);
        req.Headers.Add("webhook-timestamp", webhookTimestamp);
        req.Headers.Add("webhook-signature", signature);

        using var resp = await http.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Gather respondeu {(int)resp.StatusCode} para {type}: {respBody}");
            resp.EnsureSuccessStatusCode(); // derruba o job — 4xx não deve ser retentado às cegas
        }
        else
        {
            Console.WriteLine($"{type} -> {(int)resp.StatusCode} {respBody}");
        }
    }
}