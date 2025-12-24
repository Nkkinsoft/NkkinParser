namespace NkkinParser.Selectors;

public class Selector
{
    public CompoundSelector Compound { get; }
    public Combinator Combinator { get; }
    public Selector? Next { get; }

    public Selector(CompoundSelector compound, Combinator combinator = Combinator.Descendant, Selector? next = null)
    {
        Compound = compound;
        Combinator = combinator;
        Next = next;
    }
}

public enum AttributeOperator : byte
{
    Exists,
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith
}

public readonly struct AttributeFilter
{
    public string Name { get; }
    public string Value { get; }
    public AttributeOperator Operator { get; }

    public AttributeFilter(string name, string value, AttributeOperator op)
    {
        Name = name;
        Value = value;
        Operator = op;
    }
}

public enum PseudoClassType : byte
{
    FirstChild,
    LastChild,
    NthChild,
    Not
}

public readonly struct PseudoClassFilter
{
    public PseudoClassType Type { get; }
    public string Argument { get; } // For :nth-child(n) or :not(selector)
    public Selector? InnerSelector { get; } // For :not(selector)

    public PseudoClassFilter(PseudoClassType type, string argument = "", Selector? inner = null)
    {
        Type = type;
        Argument = argument;
        InnerSelector = inner;
    }
}

public readonly struct CompoundSelector
{
    public string TagName { get; }
    public string Id { get; }
    public string[] Classes { get; }
    public AttributeFilter[] Attributes { get; }
    public PseudoClassFilter[] PseudoClasses { get; }
    public ulong ClassBloom { get; }

    public CompoundSelector(string tagName, string id, string[] classes, AttributeFilter[]? attributes = null, PseudoClassFilter[]? pseudoClasses = null)
    {
        TagName = tagName ?? string.Empty;
        Id = id ?? string.Empty;
        Classes = classes ?? System.Array.Empty<string>();
        Attributes = attributes ?? System.Array.Empty<AttributeFilter>();
        PseudoClasses = pseudoClasses ?? System.Array.Empty<PseudoClassFilter>();
        ClassBloom = ComputeBloom(Classes);
    }

    private static ulong ComputeBloom(string[] classes)
    {
        ulong bloom = 0;
        foreach (var cls in classes)
        {
            uint h = 2166136261u;
            foreach (char c in cls) h = (h ^ c) * 16777619u;
            bloom |= 1UL << (int)(h & 63);
        }
        return bloom;
    }
}

public enum Combinator : byte { Descendant, Child, NextSibling, SubsequentSibling }
