using System.Collections.Generic;
using System.Linq;
using NkkinParser.Indexing;
using NkkinParser.Selectors;

namespace NkkinParser;

public static class DocumentExtensions
{
    public static Element? QuerySelector(this Document doc, string selector, DocumentIndex? index = null)
    {
        var sel = new CompiledSelector(selector, index);
        return sel.QuerySelectorAll(doc).FirstOrDefault();
    }

    public static List<Element> QuerySelectorAll(this Document doc, string selector, DocumentIndex? index = null)
    {
        var sel = new CompiledSelector(selector, index);
        return sel.QuerySelectorAll(doc);
    }

    public static async IAsyncEnumerable<Element> QuerySelectorAllAsync(this Document doc, string selector, DocumentIndex? index = null)
    {
        var sel = new CompiledSelector(selector, index);
        // If index available, use candidate optimization
        IEnumerable<Element> candidates = index != null ? sel.QuerySelectorAll(doc) : sel.QuerySelectorAll(doc);

        foreach (var e in candidates)
        {
            yield return e;
            await System.Threading.Tasks.Task.Yield();
        }
    }

    public static Element? QuerySelector(this Element element, string selector)
    {
        var sel = new CompiledSelector(selector);
        // Match within subtree
        return sel.QuerySelectorAll(new Document { DocumentElement = element }).FirstOrDefault();
    }
}
