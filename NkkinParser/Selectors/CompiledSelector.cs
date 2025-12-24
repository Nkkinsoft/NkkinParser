using System;
using System.Collections.Generic;
using NkkinParser.Indexing;

namespace NkkinParser.Selectors;

public sealed class CompiledSelector
{
    private readonly Selector? _root;
    private readonly DocumentIndex? _index;

    public CompiledSelector(string selector, DocumentIndex? index = null)
    {
        _root = CssSelectorParser.Parse(selector.AsSpan());
        _index = index;
    }

    public List<Element> QuerySelectorAll(Document document)
    {
        var results = new List<Element>();
        if (document.DocumentElement == null) return results;

        // If we have an index, we can optimize candidate selection
        IEnumerable<Element> candidates = _index != null ? GetCandidates() : GetAllElements(document.DocumentElement);

        if (candidates != null)
        {
            foreach (var e in candidates)
            {
                if (Matches(e)) results.Add(e);
            }
        }
        return results;
    }

    private IEnumerable<Element> GetCandidates()
    {
        if (_index == null) return Array.Empty<Element>();
        
        if (_root == null) return Array.Empty<Element>();
        
        var c = _root!.Compound;
        if (!string.IsNullOrEmpty(c.Id) && _index.TryGetById(c.Id, out var el)) return new[] { el! };
        if (c.Classes.Length > 0) return _index.GetByClass(c.Classes[0]) ?? (IEnumerable<Element>)Array.Empty<Element>();
        if (!string.IsNullOrEmpty(c.TagName) && c.TagName != "*") return _index.GetByTag(c.TagName) ?? (IEnumerable<Element>)Array.Empty<Element>();
        
        if (_index.DocumentElement == null) return Array.Empty<Element>();
        return GetAllElements(_index.DocumentElement);
    }

    private List<Element> GetAllElements(Element root)
    {
        var list = new List<Element>();
        Traverse(root, list);
        return list;
    }

    private void Traverse(Element? e, List<Element> list)
    {
        if (e == null) return;
        list.Add(e);
        for (var child = e.FirstChild; child != null; child = child.NextSibling)
        {
            if (child is Element childElement)
                Traverse(childElement, list);
        }
    }

    public bool Matches(Element element)
    {
        if (_root == null) return false;
        return MatchesInternal(element, _root);
    }

    private bool MatchesInternal(Element element, Selector selector)
    {
        if (!MatchesCompound(element, selector.Compound)) return false;

        if (selector.Next == null) return true;

        switch (selector.Combinator)
        {
            case Combinator.Descendant:
                var parent = element.Parent as Element;
                while (parent != null)
                {
                    if (MatchesInternal(parent, selector.Next)) return true;
                    parent = parent.Parent as Element;
                }
                return false;

            case Combinator.Child:
                return element.Parent is Element p && MatchesInternal(p, selector.Next);

            case Combinator.NextSibling:
                for (var sib = element.PreviousSibling; sib != null; sib = sib.PreviousSibling)
                {
                    if (sib is Element e)
                        return MatchesInternal(e, selector.Next);
                }
                return false;

            case Combinator.SubsequentSibling:
                for (var sib = element.PreviousSibling; sib != null; sib = sib.PreviousSibling)
                {
                    if (sib is Element e)
                    {
                        if (MatchesInternal(e, selector.Next)) return true;
                    }
                }
                return false;

            default:
                break;
        }
        return false;
    }

    private bool MatchesCompound(Element element, CompoundSelector compound)
    {
        if (!string.IsNullOrEmpty(compound.TagName) && compound.TagName != "*" && !string.Equals(element.TagName, compound.TagName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(compound.Id) && !string.Equals(element.Attributes.Get("id"), compound.Id, StringComparison.Ordinal))
            return false;

        if (compound.Classes.Length > 0)
        {
            var elementClass = element.Attributes.Get("class");
            if (string.IsNullOrEmpty(elementClass)) return false;
            
            var classes = elementClass.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in compound.Classes)
            {
                bool found = false;
                foreach (var c in classes)
                {
                    if (string.Equals(c, cls, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
        }

        if (compound.Attributes.Length > 0)
        {
            foreach (var attr in compound.Attributes)
            {
                var val = element.Attributes.Get(attr.Name);
                if (attr.Operator == AttributeOperator.Exists)
                {
                    if (!element.Attributes.Contains(attr.Name)) return false;
                    continue;
                }

                switch (attr.Operator)
                {
                    case AttributeOperator.Equals:
                        if (!string.Equals(val, attr.Value, StringComparison.Ordinal)) return false;
                        break;
                    case AttributeOperator.NotEquals:
                        if (string.Equals(val, attr.Value, StringComparison.Ordinal)) return false;
                        break;
                    case AttributeOperator.StartsWith:
                        if (!val.StartsWith(attr.Value, StringComparison.Ordinal)) return false;
                        break;
                    case AttributeOperator.EndsWith:
                        if (!val.EndsWith(attr.Value, StringComparison.Ordinal)) return false;
                        break;
                    case AttributeOperator.Contains:
                        if (!val.Contains(attr.Value, StringComparison.Ordinal)) return false;
                        break;
                }
            }
        }

        if (compound.PseudoClasses.Length > 0)
        {
            foreach (var pc in compound.PseudoClasses)
            {
                switch (pc.Type)
                {
                    case PseudoClassType.FirstChild:
                        for (var pre = element.PreviousSibling; pre != null; pre = pre.PreviousSibling)
                            if (pre is Element) return false;
                        break;

                    case PseudoClassType.LastChild:
                        for (var next = element.NextSibling; next != null; next = next.NextSibling)
                            if (next is Element) return false;
                        break;

                    case PseudoClassType.NthChild:
                        if (int.TryParse(pc.Argument, out int n))
                        {
                            if (GetElementIndex(element) != n) return false;
                        }
                        break;

                    case PseudoClassType.Not:
                        if (pc.InnerSelector != null && MatchesInternal(element, pc.InnerSelector))
                            return false;
                        break;
                }
            }
        }

        return true;
    }

    private int GetElementIndex(Element element)
    {
        int index = 1;
        for (var pre = element.PreviousSibling; pre != null; pre = pre.PreviousSibling)
        {
            if (pre is Element) index++;
        }
        return index;
    }
}
