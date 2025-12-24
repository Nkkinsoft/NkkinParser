using System;
using System.Text;

namespace NkkinParser;

public static class EncodingDetector
{
    public static Encoding Detect(ReadOnlySpan<byte> bytes)
    {
        // 1. Check for BOM
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF) return Encoding.GetEncoding("utf-32BE");
            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00) return Encoding.UTF32;
        }
        if (bytes.Length >= 3)
        {
            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8;
        }
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode;
            if (bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode;
        }

        // 2. Sniff meta tags in the first 1024 bytes
        int limit = Math.Min(bytes.Length, 1024);
        var snippet = bytes.Slice(0, limit);
        
        // Very basic sniffing for <meta charset="...">
        string content = Encoding.ASCII.GetString(snippet);
        
        int metaIndex = content.IndexOf("<meta", StringComparison.OrdinalIgnoreCase);
        while (metaIndex != -1)
        {
            int closeIndex = content.IndexOf('>', metaIndex);
            if (closeIndex == -1) break;

            string tag = content.Substring(metaIndex, closeIndex - metaIndex + 1);
            
            // Look for charset="..."
            int charsetIndex = tag.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
            if (charsetIndex != -1)
            {
                int equalsIndex = tag.IndexOf('=', charsetIndex);
                if (equalsIndex != -1)
                {
                    int start = equalsIndex + 1;
                    while (start < tag.Length && (char.IsWhiteSpace(tag[start]) || tag[start] == '"' || tag[start] == '\'')) start++;
                    int end = start;
                    while (end < tag.Length && !char.IsWhiteSpace(tag[end]) && tag[end] != '"' && tag[end] != '\'' && tag[end] != '/' && tag[end] != '>') end++;
                    
                    if (end > start)
                    {
                        var charsetName = tag.Substring(start, end - start);
                        try { return Encoding.GetEncoding(charsetName); } catch { }
                    }
                }
            }
            
            metaIndex = content.IndexOf("<meta", metaIndex + 5, StringComparison.OrdinalIgnoreCase);
        }

        return Encoding.UTF8;
    }
}