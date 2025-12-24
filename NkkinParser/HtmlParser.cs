using System;
using System.Collections.Generic;
using System.Text;

namespace NkkinParser;

public sealed class HtmlParser : IDisposable
{
    private readonly string _input;
    public string Input => _input;
    private readonly ArenaAllocator _arena;
    private readonly StringInterner _interner;
    private readonly Document _document;
    private readonly List<Element> _openElements = new();
    private readonly List<Element> _activeFormattingElements = new();
    private Element? _current;
    public Encoding? DetectedEncoding { get; private set; }

    public HtmlParser(string input)
    {
        _input = input ?? string.Empty;
        _arena = new ArenaAllocator();
        _interner = new StringInterner(_arena);
        _document = _arena.AllocateManaged<Document>();
        _current = null;
    }

    public HtmlParser(ReadOnlySpan<byte> data)
    {
        DetectedEncoding = EncodingDetector.Detect(data);
        var input = DetectedEncoding.GetString(data);
        if (input.Length > 0 && input[0] == '\uFEFF') input = input.Substring(1);
        _input = input;
        _arena = new ArenaAllocator();
        _interner = new StringInterner(_arena);
        _document = _arena.AllocateManaged<Document>();
        _current = null;
    }

    public static Document Parse(ReadOnlySpan<byte> data)
    {
        using var parser = new HtmlParser(data);
        return parser.Parse();
    }

    public static Document Parse(System.IO.Stream stream)
    {
        // Simple implementation: read all to memory for now
        // A production version might use a streaming decoder
        using var ms = new System.IO.MemoryStream();
        stream.CopyTo(ms);
        return Parse(ms.ToArray());
    }

    public Document Parse()
    {
        var tokenizer = new HtmlTokenizer(_input.AsSpan());

        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == HtmlTokenKind.Eof) break;

            switch (token.Kind)
            {
                case HtmlTokenKind.Text:
                    HandleText(token.Value);
                    break;

                case HtmlTokenKind.TagStart:
                    HandleStartTag(ref tokenizer, token);
                    break;

                case HtmlTokenKind.TagEnd:
                    HandleEndTag(token.Value);
                    break;

                case HtmlTokenKind.Comment:
                    var comment = _arena.AllocateManaged<Comment>();
                    comment.Data = token.Value.ToString();
                    if (_current != null) _current.AppendChild(comment);
                    else _document.AppendChild(comment);
                    break;

                case HtmlTokenKind.Doctype:
                    var doctype = _arena.AllocateManaged<Doctype>();
                    doctype.Name = token.Value.ToString();
                    _document.AppendChild(doctype);
                    break;
            }
        }
        return _document;
    }

    public static async IAsyncEnumerable<Node> ParseAsync(System.IO.Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
    {
        using var reader = new System.IO.StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) yield break;

        using var parser = new HtmlParser(text);
        var document = parser.Parse();

        // Stream nodes from the parsed document.
        var stack = new System.Collections.Generic.Stack<Node?>();
        stack.Push(document.DocumentElement);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == null) continue;
            yield return node;
            // allow consumer to interleave
            await System.Threading.Tasks.Task.Yield();

            for (var child = node.LastChild; child != null; child = child.PreviousSibling)
            {
                stack.Push(child);
            }
        }
    }

    private void HandleText(ReadOnlySpan<char> text)
    {
        string decoded = HtmlEntityDecoder.Decode(text);
        string normalized = TextNormalizer.Normalize(decoded);
        if (string.IsNullOrEmpty(normalized)) return;

        if (_current == null)
        {
            _current = AllocateElement("html");
            _document.AppendChild(_current);
            _openElements.Add(_current);
        }

        ReconstructActiveFormattingElements();

        if (IsInsideTableButNotCell())
        {
            var textNode = _arena.AllocateManaged<Text>();
            textNode.Data = normalized;
            FosterParent(textNode);
            return;
        }

        var textNodeEl = _arena.AllocateManaged<Text>();
        textNodeEl.Data = normalized;
        _current.AppendChild(textNodeEl);
    }

    private void HandleStartTag(ref HtmlTokenizer tokenizer, HtmlToken token)
    {
        var tagNameSpan = HtmlUtils.ExtractTagName(token.Value);
        var tagName = _interner.Intern(tagNameSpan);

        if (_current == null && tagName != "html")
        {
            _current = AllocateElement("html");
            _document.AppendChild(_current);
            _openElements.Add(_current);
        }

        if (tagName == "html")
        {
            if (_document.DocumentElement != null)
            {
                tokenizer.GetAttributes(token, _document.DocumentElement.Attributes, _interner);
                return;
            }
        }

        if (tagName == "a")
        {
            for (int j = _activeFormattingElements.Count - 1; j >= 0; j--)
            {
                if (_activeFormattingElements[j].TagName == "a")
                {
                    RunAdoptionAgencyAlgorithm("a");
                    break;
                }
            }
        }

        ReconstructActiveFormattingElements();

        if (tagName == "body")
        {
            foreach (var open in _openElements)
            {
                if (open.TagName == "body")
                {
                    tokenizer.GetAttributes(token, open.Attributes, _interner);
                    return;
                }
            }
        }

        // Auto-close handling
        if (tagName == "p") 
        {
            CloseTagIfOpen("p");
        }
        else if (tagName == "li") 
        {
            CloseTagIfOpen("li");
        }
        else if (tagName is "dt" or "dd") 
        {
            CloseTagIfOpen("dt");
            CloseTagIfOpen("dd");
        }
        else if (IsBlockElement(tagName))
        {
            CloseTagIfOpen("p");
        }

        var element = AllocateElement(tagName);
        tokenizer.GetAttributes(token, element.Attributes, _interner);

        if (tagName == "head" || tagName == "body")
        {
            foreach (var open in _openElements)
            {
                if (open.TagName == tagName)
                {
                    // Already returned if body/html was found above, but head might be special
                    return;
                }
            }
        }

        if (IsInsideTableButNotCell() && !IsTableTag(tagName))
        {
            FosterParent(element);
        }
        else
        {
            if (_current == null)
            {
                _document.AppendChild(element);
            }
            else
            {
                _current.AppendChild(element);
            }
        }

        if (!IsVoidElement(tagName))
        {
            _current = element;
            _openElements.Add(element);
            
            if (IsFormattingElement(tagName))
            {
                // Noah's Ark clause
                int count = 0;
                for (int j = _activeFormattingElements.Count - 1; j >= 0; j--)
                {
                    var entry = _activeFormattingElements[j];
                    if (entry.TagName == tagName && AttributesEqual(entry.Attributes, element.Attributes))
                    {
                        count++;
                        if (count == 3)
                        {
                            _activeFormattingElements.RemoveAt(j);
                            break;
                        }
                    }
                }
                _activeFormattingElements.Add(element);
            }
        }
    }

    private static bool AttributesEqual(AttributeCollection a, AttributeCollection b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            var attrA = a.GetAt(i);
            if (!b.Contains(attrA.Name) || b.Get(attrA.Name) != attrA.Value) return false;
        }
        return true;
    }

    private void HandleEndTag(ReadOnlySpan<char> tagValue)
    {
        var tagNameSpan = HtmlUtils.ExtractTagName(tagValue);
        var tagName = _interner.Intern(tagNameSpan);

        if (IsFormattingElement(tagName))
        {
            RunAdoptionAgencyAlgorithm(tagName);
            return;
        }

        for (int i = _openElements.Count - 1; i >= 0; i--)
        {
            if (_openElements[i].TagName == tagName)
            {
                _openElements.RemoveRange(i, _openElements.Count - i);
                _current = _openElements.Count > 0 ? _openElements[^1] : null;
                break;
            }
        }
    }

    private void RunAdoptionAgencyAlgorithm(string subject)
    {
        for (int i = 0; i < 8; i++)
        {
            // 1. Find the formatting element
            int fmtIdx = -1;
            for (int j = _activeFormattingElements.Count - 1; j >= 0; j--)
            {
                if (_activeFormattingElements[j].TagName == subject)
                {
                    fmtIdx = j;
                    break;
                }
            }
            if (fmtIdx == -1) return;

            var formattingElement = _activeFormattingElements[fmtIdx];
            int openIdx = _openElements.IndexOf(formattingElement);

            // 2. If formatting element is not in open elements, it's a parse error.
            if (openIdx == -1)
            {
                _activeFormattingElements.RemoveAt(fmtIdx);
                return;
            }

            // 3. Find the furthest block
            Element? furthestBlock = null;
            int furthestBlockIdx = -1;
            for (int j = openIdx + 1; j < _openElements.Count; j++)
            {
                if (IsSpecialElement(_openElements[j].TagName))
                {
                    furthestBlock = _openElements[j];
                    furthestBlockIdx = j;
                    break;
                }
            }

            // 4. If no furthest block, pop until formatting element and return
            if (furthestBlock == null)
            {
                while (_openElements.Count > openIdx)
                {
                    var removed = _openElements[^1];
                    _openElements.RemoveAt(_openElements.Count - 1);
                    if (removed == formattingElement) break;
                }
                _activeFormattingElements.RemoveAt(fmtIdx);
                _current = _openElements.Count > 0 ? _openElements[^1] : null;
                return;
            }

            // 5. Common ancestor
            var commonAncestor = _openElements[openIdx - 1];

            // 6. Bookmark
            int bookmark = fmtIdx;

            // 7. Inner loop (simplified for now but more robust than previous)
            var node = furthestBlock;
            var lastNode = furthestBlock;
            
            // For the sake of simplicity in this high-performance parser, 
            // we'll implement the core re-parenting logic of AAA.
            
            _openElements.RemoveAt(openIdx);
            _activeFormattingElements.RemoveAt(fmtIdx);

            // Re-parent furthest block
            furthestBlock.Remove();
            commonAncestor.AppendChild(furthestBlock);

            // Create new formatting element to wrap content after the "subject" end tag's misnested partner
            var newElement = AllocateElement(formattingElement.TagName);
            foreach (var attr in formattingElement.Attributes)
            {
                newElement.Attributes.Add(attr.Name, attr.Value);
            }

            // Move children of furthest block that should be under the new formatting element
            // (This is a simplified version of the node moving in steps 13-16)
            while (furthestBlock.FirstChild != null)
            {
                var child = furthestBlock.FirstChild;
                child.Remove();
                newElement.AppendChild(child);
            }
            furthestBlock.AppendChild(newElement);

            _current = _openElements[^1];
        }
    }

    private static bool IsSpecialElement(string tag)
    {
        return tag is "address" or "area" or "article" or "aside" or "base" or "basefont" or "bgsound" or "blockquote" or "body" or "br" or "button" or "caption" or "center" or "col" or "colgroup" or "dd" or "details" or "dir" or "div" or "dl" or "dt" or "embed" or "fieldset" or "figcaption" or "figure" or "footer" or "form" or "frame" or "frameset" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "head" or "header" or "hgroup" or "hr" or "html" or "iframe" or "img" or "input" or "keygen" or "li" or "link" or "listing" or "main" or "marquee" or "menu" or "meta" or "nav" or "noembed" or "noframes" or "noscript" or "object" or "ol" or "p" or "param" or "plaintext" or "pre" or "script" or "section" or "select" or "source" or "style" or "summary" or "table" or "tbody" or "td" or "tfoot" or "th" or "thead" or "title" or "tr" or "track" or "ul" or "wbr" or "xmp";
    }

    private bool IsInsideTableButNotCell()
    {
        if (_current == null) return false;
        var tag = _current.TagName;
        return (tag == "table" || tag == "tbody" || tag == "tfoot" || tag == "thead" || tag == "tr");
    }

    private void FosterParent(Node node)
    {
        for (int i = _openElements.Count - 1; i >= 0; i--)
        {
            if (_openElements[i].TagName == "table")
            {
                var table = _openElements[i];
                if (table.Parent != null)
                {
                    table.Parent.InsertBefore(node, table);
                }
                else
                {
                    _document.AppendChild(node);
                }
                return;
            }
        }
        _document.AppendChild(node);
    }


    private void ReconstructActiveFormattingElements()
    {
        if (_activeFormattingElements.Count == 0) return;

        int idx = _activeFormattingElements.Count - 1;
        var entry = _activeFormattingElements[idx];
        if (_openElements.Contains(entry)) return;

        while (idx > 0)
        {
            var prev = _activeFormattingElements[idx - 1];
            if (_openElements.Contains(prev)) break;
            idx--;
        }

        while (idx < _activeFormattingElements.Count)
        {
            entry = _activeFormattingElements[idx];
            var newElement = AllocateElement(entry.TagName);
            foreach (var attr in entry.Attributes)
            {
                newElement.Attributes.Add(attr.Name, attr.Value);
            }

            _current!.AppendChild(newElement);
            _current = newElement;
            _openElements.Add(newElement);
            _activeFormattingElements[idx] = newElement; 
            idx++;
        }
    }

    private static bool IsVoidElement(string tag)
    {
        return tag is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";
    }

    private static bool IsFormattingElement(string tag)
    {
        return tag is "a" or "b" or "big" or "code" or "em" or "font" or "i" or "nobr" or "s" or "small" or "strike" or "strong" or "tt" or "u";
    }

    public void Dispose() => _arena.Dispose();

    private void CloseTagIfOpen(string tagName)
    {
        for (int i = _openElements.Count - 1; i >= 0; i--)
        {
            if (_openElements[i].TagName == tagName)
            {
                _openElements.RemoveRange(i, _openElements.Count - i);
                _current = _openElements.Count > 0 ? _openElements[^1] : null;
                break;
            }
        }
    }

    private static bool IsBlockElement(string tag)
    {
        return tag is "address" or "article" or "aside" or "blockquote" or "details" or "div" or "dl" or "fieldset" or "figcaption" or "figure" or "footer" or "form" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "header" or "hgroup" or "hr" or "main" or "menu" or "nav" or "ol" or "p" or "pre" or "section" or "table" or "ul";
    }

    private static bool IsTableTag(string tag)
    {
        return tag is "table" or "tbody" or "thead" or "tfoot" or "tr" or "td" or "th" or "caption" or "colgroup" or "col";
    }

    private Element AllocateElement(string tagName)
    {
        var element = _arena.AllocateManaged<Element>();
        element.TagName = tagName;
        return element;
    }
}
