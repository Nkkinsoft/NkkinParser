using System.Collections.Generic;
using NkkinParser;

namespace NkkinParser.Indexing;

public sealed class DocumentIndex
{
    private readonly Document _document;
    private readonly Dictionary<string, Element> _ids = new();
    private readonly Dictionary<string, List<Element>> _classes = new();
    private readonly Dictionary<string, List<Element>> _tags = new();

    public Element? DocumentElement => _document.DocumentElement;

    public DocumentIndex(Document document, ArenaAllocator arena)
    {
        _document = document;
        Index(document.DocumentElement);
    }

    private void Index(Element? element)
    {
        if (element is null) return;

        var id = element.Attributes.Get("id");
        if (!string.IsNullOrEmpty(id)) _ids[id] = element;

        var cls = element.Attributes.Get("class");
        if (!string.IsNullOrEmpty(cls))
        {
            foreach (var c in cls.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!_classes.TryGetValue(c, out var list))
                    _classes[c] = list = new();
                list.Add(element);
            }
        }

        if (!_tags.TryGetValue(element.TagName, out var tagList))
            _tags[element.TagName] = tagList = new();
        tagList.Add(element);

        for (var child = element.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child is Element childElement)
                Index(childElement);
        }
    }

    public bool TryGetById(string id, out Element? element) => _ids.TryGetValue(id, out element);
    public List<Element>? GetByClass(string cls) => _classes.GetValueOrDefault(cls);
    public List<Element>? GetByTag(string tag) => _tags.GetValueOrDefault(tag);
}
