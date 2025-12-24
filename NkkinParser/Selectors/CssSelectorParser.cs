using System;
using System.Collections.Generic;

namespace NkkinParser.Selectors;

public sealed class CssSelectorParser
{
    public static Selector? Parse(ReadOnlySpan<char> selector)
    {
        selector = selector.Trim();
        if (selector.IsEmpty) return null;

        Selector? current = null;

        int i = 0;
        while (i < selector.Length)
        {
            // Parse compound selector
            int start = i;
            Combinator combinator = Combinator.Descendant;

            // Handle combinators
            if (i > 0)
            {
                char c = selector[i];
                if (c == '>' || c == '+' || c == '~')
                {
                    combinator = c switch {
                        '>' => Combinator.Child,
                        '+' => Combinator.NextSibling,
                        '~' => Combinator.SubsequentSibling,
                        _ => Combinator.Descendant
                    };
                    i++;
                    while (i < selector.Length && char.IsWhiteSpace(selector[i])) i++;
                    start = i;
                }
            }

            // Find end of compound
            bool inBrackets = false;
            char inQuote = '\0';

            while (i < selector.Length)
            {
                char c = selector[i];
                
                if (inQuote != '\0')
                {
                    if (c == inQuote) inQuote = '\0';
                }
                else if (c == '\'' || c == '\"')
                {
                    inQuote = c;
                }
                else if (c == '[')
                {
                    inBrackets = true;
                }
                else if (c == ']')
                {
                    inBrackets = false;
                }
                else if (!inBrackets)
                {
                    if (c == '>' || c == '+' || c == '~') break;
                    if (char.IsWhiteSpace(c))
                    {
                        // Peek ahead to see if it's a combinator
                        int next = i;
                        while (next < selector.Length && char.IsWhiteSpace(selector[next])) next++;
                        if (next < selector.Length && (selector[next] == '>' || selector[next] == '+' || selector[next] == '~'))
                        {
                            // Whitespace followed by combinator is ignored as a separator
                            i = next; 
                            continue; // Re-evaluate at combinator
                        }
                        else
                        {
                            // Whitespace separator means next is a descendant
                            break;
                        }
                    }
                }
                i++;
            }

            var length = i - start;
            if (length <= 0) break; // Should not happen but for safety

            var compoundSpan = selector.Slice(start, length).Trim();
            var compound = ParseCompound(compoundSpan);
            
            // Build linked list: rightmost is the root
            // For "div > p", it generates: p (root) -> div (next) with 'Child' combinator on p.
            // Wait, the combinator belongs to the relationship BEFORE the current compound if parsing left-to-right.
            // Let's rethink: current = new Selector(compound, Next = previous, Combinator = the one we found before this compound)
            current = new Selector(compound, combinator, current);

            while (i < selector.Length && char.IsWhiteSpace(selector[i])) i++;
        }

        return current;
    }

    private static CompoundSelector ParseCompound(ReadOnlySpan<char> span)
    {
        string tagName = string.Empty;
        string id = string.Empty;
        List<string> classes = new();
        List<AttributeFilter> attributes = new();
        List<PseudoClassFilter> pseudoClasses = new();

        int i = 0;
        if (i < span.Length && (char.IsLetter(span[i]) || span[i] == '*' || span[i] == '_'))
        {
            int start = i;
            while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '-' || span[i] == '_')) i++;
            tagName = span.Slice(start, i - start).ToString();
        }

        while (i < span.Length)
        {
            char c = span[i];
            if (c == '#')
            {
                i++;
                int start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '-' || span[i] == '_')) i++;
                id = span.Slice(start, i - start).ToString();
            }
            else if (c == '.')
            {
                i++;
                int start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '-' || span[i] == '_')) i++;
                classes.Add(span.Slice(start, i - start).ToString());
            }
            else if (c == '[')
            {
                i++;
                // Skip whitespace
                while (i < span.Length && char.IsWhiteSpace(span[i])) i++;
                
                int nameStart = i;
                while (i < span.Length && !char.IsWhiteSpace(span[i]) && span[i] != '=' && span[i] != ']' && span[i] != '*' && span[i] != '^' && span[i] != '$' && span[i] != '~' && span[i] != '|') i++;
                string attrName = span.Slice(nameStart, i - nameStart).ToString();
                
                while (i < span.Length && char.IsWhiteSpace(span[i])) i++;

                if (i < span.Length && span[i] == ']')
                {
                    attributes.Add(new AttributeFilter(attrName, string.Empty, AttributeOperator.Exists));
                    i++;
                }
                else if (i < span.Length)
                {
                    AttributeOperator op = AttributeOperator.Equals;
                    if (span[i] == '=') { op = AttributeOperator.Equals; i++; }
                    else if (i + 1 < span.Length)
                    {
                        char c1 = span[i];
                        char c2 = span[i+1];
                        if (c1 == '~' && c2 == '=') { op = AttributeOperator.Contains; i += 2; }
                        else if (c1 == '^' && c2 == '=') { op = AttributeOperator.StartsWith; i += 2; }
                        else if (c1 == '$' && c2 == '=') { op = AttributeOperator.EndsWith; i += 2; }
                        else if (c1 == '*' && c2 == '=') { op = AttributeOperator.Contains; i += 2; }
                        else if (c1 == '!' && c2 == '=') { op = AttributeOperator.NotEquals; i += 2; }
                        else { i++; } // Skip unknown op char
                    }
                    else { i++; }

                    while (i < span.Length && char.IsWhiteSpace(span[i])) i++;

                    string val;
                    if (i < span.Length && (span[i] == '\"' || span[i] == '\''))
                    {
                        char quote = span[i];
                        i++;
                        int valStart = i;
                        while (i < span.Length && span[i] != quote) i++;
                        val = span.Slice(valStart, i - valStart).ToString();
                        if (i < span.Length) i++; // skip quote
                    }
                    else
                    {
                        int valStart = i;
                        while (i < span.Length && span[i] != ']' && !char.IsWhiteSpace(span[i])) i++;
                        val = span.Slice(valStart, i - valStart).ToString();
                    }

                    attributes.Add(new AttributeFilter(attrName, val, op));
                    
                    while (i < span.Length && span[i] != ']') i++;
                    if (i < span.Length) i++; // Skip ]
                }
            }
            else if (c == ':')
            {
                i++;
                int start = i;
                while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '-')) i++;
                string name = span.Slice(start, i - start).ToString().ToLowerInvariant();

                if (i < span.Length && span[i] == '(')
                {
                    // Functional pseudo-class
                    i++;
                    int argStart = i;
                    int depth = 1;
                    while (i < span.Length && depth > 0)
                    {
                        if (span[i] == '(') depth++;
                        else if (span[i] == ')') depth--;
                        if (depth > 0) i++;
                    }
                    string arg = span.Slice(argStart, i - argStart).ToString().Trim();
                    if (i < span.Length) i++; // Skip ')'

                    if (name == "nth-child")
                    {
                        pseudoClasses.Add(new PseudoClassFilter(PseudoClassType.NthChild, arg));
                    }
                    else if (name == "not")
                    {
                        var inner = Parse(arg.AsSpan());
                        pseudoClasses.Add(new PseudoClassFilter(PseudoClassType.Not, arg, inner));
                    }
                }
                else
                {
                    if (name == "first-child") pseudoClasses.Add(new PseudoClassFilter(PseudoClassType.FirstChild));
                    else if (name == "last-child") pseudoClasses.Add(new PseudoClassFilter(PseudoClassType.LastChild));
                }
            }
            else
            {
                i++;
            }
        }

        return new CompoundSelector(tagName, id, classes.ToArray(), attributes.ToArray(), pseudoClasses.ToArray());
    }
}
