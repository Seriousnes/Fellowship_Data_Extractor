using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fellowship.SDK.Generators;

internal enum JsonKind
{
    Object,
    Array,
    String,
    Number,
    Word,
}

/// <summary>One value out of the vocabulary, carrying what the reads this generator makes need.</summary>
internal sealed class JsonValue
{
    private static readonly JsonValue[] NoItems = [];
    private static readonly KeyValuePair<string, JsonValue>[] NoMembers = [];

    public JsonValue(JsonKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public JsonValue(IReadOnlyList<JsonValue> items)
    {
        Kind = JsonKind.Array;
        Items = items;
    }

    public JsonValue(IReadOnlyList<KeyValuePair<string, JsonValue>> members)
    {
        Kind = JsonKind.Object;
        Members = members;
    }

    public JsonKind Kind { get; }

    /// <summary>A string's content or a number's digits, as written.</summary>
    public string Text { get; } = string.Empty;

    public IReadOnlyList<JsonValue> Items { get; } = NoItems;

    public IReadOnlyList<KeyValuePair<string, JsonValue>> Members { get; } = NoMembers;

    /// <summary>The member under a name, or null where there is none and where this is not an object.</summary>
    public JsonValue? this[string name]
    {
        get
        {
            foreach (var member in Members)
            {
                if (string.Equals(member.Key, name, StringComparison.OrdinalIgnoreCase)) return member.Value;
            }

            return null;
        }
    }
}

/// <summary>
/// Reads the vocabulary without a package dependency. An analyzer is loaded into the compiler's own
/// process, and a package the compiler host does not itself carry resolves in some hosts and not in
/// others; the compiler that runs the command line and the compiler that runs inside an editor are
/// not the same process on the same runtime. Reading the file here is what makes the generator load
/// the same way everywhere.
/// </summary>
internal sealed class JsonReader
{
    private readonly string _text;
    private int _at;

    private JsonReader(string text) => _text = text;

    public static JsonValue Parse(string text)
    {
        var reader = new JsonReader(text);
        var value = reader.ReadValue();
        reader.SkipWhitespace();
        if (reader._at != text.Length) throw reader.Failure("content follows the value the document states");

        return value;
    }

    private JsonValue ReadValue()
    {
        SkipWhitespace();
        if (_at >= _text.Length) throw Failure("the document ends where a value was expected");

        return _text[_at] switch
        {
            '{' => ReadObject(),
            '[' => ReadArray(),
            '"' => new JsonValue(JsonKind.String, ReadString()),
            't' => ReadWord("true"),
            'f' => ReadWord("false"),
            'n' => ReadWord("null"),
            _ => ReadNumber(),
        };
    }

    private JsonValue ReadObject()
    {
        _at++;
        var members = new List<KeyValuePair<string, JsonValue>>();

        SkipWhitespace();
        if (Peek() == '}')
        {
            _at++;
            return new JsonValue(members);
        }

        while (true)
        {
            SkipWhitespace();
            if (Peek() != '"') throw Failure("a member name was expected");

            var name = ReadString();
            SkipWhitespace();
            if (Peek() != ':') throw Failure($"a colon was expected after the member \"{name}\"");

            _at++;
            members.Add(new KeyValuePair<string, JsonValue>(name, ReadValue()));

            SkipWhitespace();
            if (Peek() == ',')
            {
                _at++;
                continue;
            }

            if (Peek() == '}')
            {
                _at++;
                return new JsonValue(members);
            }

            throw Failure("a comma or a closing brace was expected");
        }
    }

    private JsonValue ReadArray()
    {
        _at++;
        var items = new List<JsonValue>();

        SkipWhitespace();
        if (Peek() == ']')
        {
            _at++;
            return new JsonValue(items);
        }

        while (true)
        {
            items.Add(ReadValue());

            SkipWhitespace();
            if (Peek() == ',')
            {
                _at++;
                continue;
            }

            if (Peek() == ']')
            {
                _at++;
                return new JsonValue(items);
            }

            throw Failure("a comma or a closing bracket was expected");
        }
    }

    private string ReadString()
    {
        _at++;
        var text = new StringBuilder();

        while (true)
        {
            if (_at >= _text.Length) throw Failure("a string is left unclosed");

            var character = _text[_at++];
            if (character == '"') return text.ToString();
            if (character != '\\')
            {
                text.Append(character);
                continue;
            }

            if (_at >= _text.Length) throw Failure("a string ends inside an escape");

            var escaped = _text[_at++];
            switch (escaped)
            {
                case '"': text.Append('"'); break;
                case '\\': text.Append('\\'); break;
                case '/': text.Append('/'); break;
                case 'b': text.Append('\b'); break;
                case 'f': text.Append('\f'); break;
                case 'n': text.Append('\n'); break;
                case 'r': text.Append('\r'); break;
                case 't': text.Append('\t'); break;
                case 'u':
                    if (_at + 4 > _text.Length) throw Failure("a string ends inside a \\u escape");
                    if (!ushort.TryParse(
                            _text.Substring(_at, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out var point))
                    {
                        throw Failure($"\\u{_text.Substring(_at, 4)} is not four hexadecimal digits");
                    }

                    text.Append((char)point);
                    _at += 4;
                    break;
                default: throw Failure($"\\{escaped} is not an escape a string can carry");
            }
        }
    }

    private JsonValue ReadNumber()
    {
        var start = _at;
        if (Peek() is '-' or '+') _at++;

        while (_at < _text.Length && (char.IsDigit(_text[_at]) || _text[_at] is '.' or 'e' or 'E' or '+' or '-')) _at++;
        if (_at == start) throw Failure($"'{_text[start]}' does not begin a value");

        return new JsonValue(JsonKind.Number, _text.Substring(start, _at - start));
    }

    private JsonValue ReadWord(string word)
    {
        if (_at + word.Length > _text.Length || string.CompareOrdinal(_text, _at, word, 0, word.Length) != 0)
        {
            throw Failure($"\"{word}\" was expected");
        }

        _at += word.Length;
        return new JsonValue(JsonKind.Word, word);
    }

    private void SkipWhitespace()
    {
        while (_at < _text.Length && char.IsWhiteSpace(_text[_at])) _at++;
    }

    private char Peek() => _at < _text.Length ? _text[_at] : '\0';

    private FormatException Failure(string reason)
    {
        var line = 1;
        for (var at = 0; at < _at && at < _text.Length; at++)
        {
            if (_text[at] == '\n') line++;
        }

        return new FormatException($"line {line}: {reason}");
    }
}
