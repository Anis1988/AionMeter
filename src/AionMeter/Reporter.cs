using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AionMeter;

/// <summary>
/// Sends the unread-lines file to a private GitHub repo, so the parser can be fixed from it.
/// Only lines with a damage/HP number the meter couldn't read are sent, never chat.
/// </summary>
public static class Reporter
{
    const string RemotePath = "unread-lines.log";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AionMeter", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    /// <summary>Encrypts the token for this Windows user before it's saved in settings.</summary>
    public static string Protect(string token) =>
        token.Length == 0 ? "" : Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));

    public static string Unprotect(string stored)
    {
        if (stored.Length == 0)
            return "";
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return "";
        }
    }

    /// <summary>Lines are the same report when only their time and numbers differ.</summary>
    static string Shape(string line) =>
        Regex.Replace(line.Length > 22 ? line[22..] : line, @"\d[\d,]*", "#").Trim();

    /// <summary>
    /// Adds the local unread lines that aren't in the repo's file yet. Returns a short status message.
    /// </summary>
    public static async Task<string> SendAsync(string repo, string token, string localFile)
    {
        if (repo.Length == 0 || token.Length == 0)
            return "Set the repo and token first";
        if (!File.Exists(localFile))
            return "Nothing to send";

        var url = $"https://api.github.com/repos/{repo}/contents/{RemotePath}";
        using var get = new HttpRequestMessage(HttpMethod.Get, url);
        get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var got = await Http.SendAsync(get);

        string? sha = null;
        var remote = "";
        if (got.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await got.Content.ReadAsStringAsync());
            sha = doc.RootElement.GetProperty("sha").GetString();
            remote = Encoding.UTF8.GetString(Convert.FromBase64String(
                doc.RootElement.GetProperty("content").GetString()!.Replace("\n", "")));
        }
        else if (got.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            return $"GitHub said {(int)got.StatusCode} - check the repo name and token";
        }

        var known = remote.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Shape).ToHashSet();
        var fresh = (await File.ReadAllLinesAsync(localFile))
            .Where(l => l.Trim().Length > 0 && known.Add(Shape(l)))
            .ToList();
        if (fresh.Count == 0)
            return "Up to date";

        var content = remote.TrimEnd('\n') + (remote.Length > 0 ? "\n" : "") + string.Join("\n", fresh) + "\n";
        var body = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["message"] = $"Add {fresh.Count} unread line(s) from Aion Meter",
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["sha"] = sha,
        });
        using var put = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var sent = await Http.SendAsync(put);
        return sent.IsSuccessStatusCode
            ? $"Sent {fresh.Count} line(s)"
            : $"GitHub said {(int)sent.StatusCode} - check the token can write to {repo}";
    }
}
