using System;
using System.Text;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NkkinParser;

public enum NodeType : byte { None, Document, Element, EndElement, Text, Comment, Doctype }

public abstract class Node
{
    public NodeType NodeType { get; init; }
    public Node? Parent { get; internal set; }
    public Node? FirstChild { get; internal set; }
    public Node? LastChild { get; internal set; }
    public Node? NextSibling { get; internal set; }
    public Node? PreviousSibling { get; internal set; }

    public string OuterHtml => HtmlSerializer.Serialize(this, true);
    public virtual string TextContent => string.Empty;

    internal virtual void AppendChild(Node child)
    {
        if (child.Parent != null) child.Remove();
        
        child.Parent = this;
        if (LastChild is null)
        {
            FirstChild = LastChild = child;
        }
        else
        {
            child.PreviousSibling = LastChild;
            LastChild.NextSibling = child;
            LastChild = child;
        }
    }

    internal virtual void InsertBefore(Node child, Node reference)
    {
        if (reference == null)
        {
            AppendChild(child);
            return;
        }

        child.Remove();
        child.Parent = this;
        child.NextSibling = reference;
        child.PreviousSibling = reference.PreviousSibling;

        if (reference.PreviousSibling == null)
        {
            FirstChild = child;
        }
        else
        {
            reference.PreviousSibling.NextSibling = child;
        }

        reference.PreviousSibling = child;
    }

    internal void Remove()
    {
        if (Parent == null) return;
        if (Parent.FirstChild == this) Parent.FirstChild = NextSibling;
        if (Parent.LastChild == this) Parent.LastChild = PreviousSibling;
        if (NextSibling != null) NextSibling.PreviousSibling = PreviousSibling;
        if (PreviousSibling != null) PreviousSibling.NextSibling = NextSibling;
        Parent = null;
        NextSibling = null;
        PreviousSibling = null;
    }
}

public sealed class Document : Node
{
    public Element? DocumentElement { get; internal set; }
    public Document() => NodeType = NodeType.Document;

    internal override void AppendChild(Node child)
    {
        base.AppendChild(child);
        if (child is Element ele && DocumentElement == null)
        {
            DocumentElement = ele;
        }
    }
}

public sealed class Element : Node
{
    public string TagName { get; set; } = string.Empty;
    public AttributeCollection Attributes { get; } = new();

    public Element() => NodeType = NodeType.Element;

    public string InnerHtml => HtmlSerializer.Serialize(this, false);

    public override string TextContent
    {
        get
        {
            var sb = new StringBuilder();
            BuildTextContent(this, sb);
            return sb.ToString();
        }
    }

    private static void BuildTextContent(Node node, StringBuilder sb)
    {
        if (node is Text text)
        {
            sb.Append(text.Data);
        }
        else if (node is Element element)
        {
            for (var child = element.FirstChild; child != null; child = child.NextSibling)
            {
                BuildTextContent(child, sb);
            }
        }
    }
}

public sealed class Text : Node
{
    public string Data { get; set; } = string.Empty;
    public Text() => NodeType = NodeType.Text;
    public override string TextContent => Data;
}

public sealed class Comment : Node
{
    public string Data { get; set; } = string.Empty;
    public Comment() => NodeType = NodeType.Comment;
}

public sealed class Doctype : Node
{
    public string Name { get; set; } = string.Empty;
    public Doctype() => NodeType = NodeType.Doctype;
}

public struct HtmlAttribute
{
    public string Name;
    public string Value;
}

internal struct StoredAttribute
{
    public string Name;
    public Memory<char> Value;
}

public class AttributeCollection : IEnumerable<HtmlAttribute>
{
    // array of stored attributes (names + memory slices)
    private StoredAttribute[] _items = new StoredAttribute[4];
    private int _count;
    private bool _itemsRented;

    // pooled char buffer for attribute values
    private char[]? _valueBuffer;
    private int _valuePos;
    private bool _valueBufferRented;

    public int Count => _count;

    // Add attribute from a ReadOnlySpan<char> directly into the pooled value buffer
    public void Add(string name, ReadOnlySpan<char> value)
    {
        EnsureItemsCapacity();

        int len = value.Length;
        EnsureValueCapacity(_valuePos + len);

        // copy value into buffer
        value.CopyTo(new Span<char>(_valueBuffer!, _valuePos, len));
        var mem = new Memory<char>(_valueBuffer!, _valuePos, len);
        _valuePos += len;

        _items[_count++] = new StoredAttribute { Name = name, Value = mem };
    }

    // Compatibility overload
    public void Add(string name, string value) => Add(name, value.AsSpan());

    private void EnsureItemsCapacity()
    {
        if (_count == _items.Length)
        {
            int newSize = Math.Max(4, _items.Length * 2);
            var pool = ArrayPool<StoredAttribute>.Shared;
            var newArr = pool.Rent(newSize);
            Array.Copy(_items, 0, newArr, 0, _items.Length);
            if (_itemsRented) ArrayPool<StoredAttribute>.Shared.Return(_items, clearArray: true);
            _items = newArr;
            _itemsRented = true;
        }
    }

    private void EnsureValueCapacity(int required)
    {
        if (_valueBuffer == null)
        {
            int size = Math.Max(1024, required);
            _valueBuffer = ArrayPool<char>.Shared.Rent(size);
            _valueBufferRented = true;
            _valuePos = 0;
            return;
        }

        if (required > _valueBuffer.Length)
        {
            int newSize = Math.Max(required, _valueBuffer.Length * 2);
            var newBuf = ArrayPool<char>.Shared.Rent(newSize);
            Array.Copy(_valueBuffer, 0, newBuf, 0, _valuePos);
            if (_valueBufferRented) ArrayPool<char>.Shared.Return(_valueBuffer, clearArray: true);
            _valueBuffer = newBuf;
            _valueBufferRented = true;
        }
    }

    public (string Name, string Value) GetAt(int index)
    {
        var stored = _items[index];
        return (stored.Name, new string(stored.Value.Span));
    }

    public IEnumerator<HtmlAttribute> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            var s = _items[i];
            yield return new HtmlAttribute { Name = s.Name, Value = new string(s.Value.Span) };
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(string name)
    {
        for (int i = 0; i < _count; i++)
        {
            if (string.Equals(_items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public string Get(string name)
    {
        for (int i = 0; i < _count; i++)
        {
            if (string.Equals(_items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return new string(_items[i].Value.Span);
        }
        return string.Empty;
    }

    // Clear the collection and return any rented buffers to the pool.
    public void Clear()
    {
        if (_itemsRented)
        {
            ArrayPool<StoredAttribute>.Shared.Return(_items, clearArray: true);
            _items = new StoredAttribute[4];
            _itemsRented = false;
        }
        else
        {
            Array.Clear(_items, 0, _items.Length);
        }

        if (_valueBufferRented && _valueBuffer != null)
        {
            ArrayPool<char>.Shared.Return(_valueBuffer, clearArray: true);
            _valueBuffer = null;
            _valueBufferRented = false;
        }
        else if (_valueBuffer != null)
        {
            Array.Clear(_valueBuffer, 0, _valueBuffer.Length);
            _valueBuffer = null;
        }

        _count = 0;
        _valuePos = 0;
    }
}
