using System;
using System.Text;
using System.Buffers;
using System.IO;

namespace NkkinParser;

public static class HtmlSerializer
{
    public static string Serialize(Node node, bool outer = false)
    {
        Span<char> initialBuffer = stackalloc char[1024];
        var builder = new ValueStringBuilder(initialBuffer);
        
        try
        {
            SerializeIterative(ref builder, node, outer);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void SerializeIterative(ref ValueStringBuilder builder, Node root, bool outer)
    {
        var stack = new System.Collections.Generic.Stack<(Node Node, bool IsEnd, bool Outer)>();
        stack.Push((root, false, outer));

        while (stack.Count > 0)
        {
            var (node, isEnd, isOuter) = stack.Pop();

            if (node is Element el)
            {
                if (isEnd)
                {
                    if (isOuter && !IsVoidElement(el.TagName))
                    {
                        builder.Append("</");
                        builder.Append(el.TagName);
                        builder.Append('>');
                    }
                }
                else
                {
                    if (isOuter)
                    {
                        builder.Append('<');
                        builder.Append(el.TagName);
                        for (int i = 0; i < el.Attributes.Count; i++)
                        {
                            var attr = el.Attributes.GetAt(i);
                            builder.Append(' ');
                            builder.Append(attr.Name);
                            builder.Append("=\"");
                            EncodeAttributeValue(ref builder, attr.Value);
                            builder.Append('\"');
                        }
                        builder.Append('>');
                    }

                    // Push End tag if needed
                    if (isOuter && !IsVoidElement(el.TagName))
                    {
                        stack.Push((el, true, true));
                    }

                    // Push children in reverse order to process them in correct order
                    for (var child = el.LastChild; child != null; child = child.PreviousSibling)
                    {
                        stack.Push((child, false, true));
                    }
                }
            }
            else if (node is Text text)
            {
                builder.Append(text.Data);
            }
            else if (node is Comment comment)
            {
                builder.Append("<!--");
                builder.Append(comment.Data);
                builder.Append("-->");
            }
            else if (node is Document doc)
            {
                for (var child = doc.LastChild; child != null; child = child.PreviousSibling)
                {
                    stack.Push((child, false, true));
                }
            }
            else if (node is Doctype doctype)
            {
                builder.Append("<!DOCTYPE ");
                builder.Append(doctype.Name);
                builder.Append('>');
            }
        }
    }

    private static void EncodeAttributeValue(ref ValueStringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        foreach (char c in value)
        {
            if (c == '\"') builder.Append("&quot;");
            else if (c == '&') builder.Append("&amp;");
            else if (c == '<') builder.Append("&lt;");
            else if (c == '>') builder.Append("&gt;");
            else builder.Append(c);
        }
    }

    private static bool IsVoidElement(string tag)
    {
        return tag is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
    }
}
