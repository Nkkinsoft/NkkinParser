using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace NkkinParser;

public static class HtmlLoader
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

    public static async Task<Document> LoadAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        
        // We'll buffer into a string for the current implementation of HtmlParser
        // but high-performance streaming could be implemented directly on the stream.
        using var reader = new StreamReader(stream);
        string html = await reader.ReadToEndAsync();
        
        var parser = new HtmlParser(html);
        return parser.Parse();
    }
}