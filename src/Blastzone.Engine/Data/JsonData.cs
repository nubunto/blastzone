using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Engine.Data;

/// <summary>Malformed or incomplete Game data. The message names the file and the key path.</summary>
public sealed class GameDataException(string message) : Exception(message);

/// <summary>
/// A parsed JSON value, read with <see cref="Utf8JsonReader"/> and no reflection (ADR 0002, ADR 0006).
/// Objects remember which keys were read, so unknown keys (usually typos) can be reported.
/// Numbers keep their raw text so they convert to <see cref="Fixed"/> exactly (ADR 0004).
/// </summary>
public sealed class JsonData
{
    enum Kind { Object, Array, Number, String, Bool, Null }

    readonly Kind _kind;
    readonly string _source;
    readonly List<KeyValuePair<string, JsonData>>? _members;
    readonly HashSet<string>? _read;
    readonly List<JsonData>? _items;
    readonly byte[]? _number;
    readonly string? _string;
    readonly bool _bool;

    /// <summary>Dotted key path from the root, for error messages.</summary>
    public string Path { get; }

    JsonData(Kind kind, string source, string path, List<KeyValuePair<string, JsonData>>? members = null,
        List<JsonData>? items = null, byte[]? number = null, string? text = null, bool flag = false)
    {
        _kind = kind;
        _source = source;
        Path = path;
        _members = members;
        _read = members is null ? null : new HashSet<string>(StringComparer.Ordinal);
        _items = items;
        _number = number;
        _string = text;
        _bool = flag;
    }

    /// <param name="source">File name or other label used in error messages.</param>
    public static JsonData Parse(ReadOnlySpan<byte> utf8, string source)
    {
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        try
        {
            if (!reader.Read())
                throw new GameDataException($"{source}: empty file.");
            var root = ReadValue(ref reader, source, "");
            if (reader.Read())
                throw new GameDataException($"{source}: unexpected content after the root value.");
            return root;
        }
        catch (JsonException e)
        {
            throw new GameDataException($"{source}: invalid JSON at line {e.LineNumber + 1}: {e.Message}");
        }
    }

    static JsonData ReadValue(ref Utf8JsonReader reader, string source, string path)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var members = new List<KeyValuePair<string, JsonData>>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString()!;
                    var childPath = path.Length == 0 ? key : path + "." + key;
                    if (members.Exists(m => m.Key == key))
                        throw new GameDataException($"{source}: duplicate key '{childPath}'.");
                    reader.Read();
                    members.Add(new(key, ReadValue(ref reader, source, childPath)));
                }
                return new JsonData(Kind.Object, source, path, members: members);
            case JsonTokenType.StartArray:
                var items = new List<JsonData>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    items.Add(ReadValue(ref reader, source, $"{path}[{items.Count}]"));
                return new JsonData(Kind.Array, source, path, items: items);
            case JsonTokenType.Number:
                return new JsonData(Kind.Number, source, path, number: reader.ValueSpan.ToArray());
            case JsonTokenType.String:
                return new JsonData(Kind.String, source, path, text: reader.GetString());
            case JsonTokenType.True:
            case JsonTokenType.False:
                return new JsonData(Kind.Bool, source, path, flag: reader.TokenType == JsonTokenType.True);
            case JsonTokenType.Null:
                return new JsonData(Kind.Null, source, path);
            default:
                throw new GameDataException($"{source}: unexpected {reader.TokenType} at '{path}'.");
        }
    }

    GameDataException Error(string message) => new($"{_source}: '{Path}' {message}");

    JsonData Expect(Kind kind)
    {
        if (_kind != kind)
            throw Error($"must be {kind.ToString().ToLowerInvariant()}, not {_kind.ToString().ToLowerInvariant()}.");
        return this;
    }

    public bool Has(string key) => Expect(Kind.Object)._members!.Exists(m => m.Key == key);

    /// <summary>The member <paramref name="key"/> of this object; missing keys are errors.</summary>
    public JsonData this[string key]
    {
        get
        {
            Expect(Kind.Object);
            foreach (var member in _members!)
            {
                if (member.Key != key)
                    continue;
                _read!.Add(key);
                return member.Value;
            }
            throw new GameDataException($"{_source}: missing '{(Path.Length == 0 ? key : Path + "." + key)}'.");
        }
    }

    public IReadOnlyList<JsonData> Items => Expect(Kind.Array)._items!;

    /// <summary>The members of this object, in file order, all marked as read.</summary>
    public IEnumerable<KeyValuePair<string, JsonData>> Members
    {
        get
        {
            Expect(Kind.Object);
            foreach (var member in _members!)
            {
                _read!.Add(member.Key);
                yield return member;
            }
        }
    }

    public Fixed Fixed
    {
        get
        {
            Expect(Kind.Number);
            try
            {
                return FixedDecimal.Parse(_number);
            }
            catch (FormatException e)
            {
                throw Error($"is not a plain decimal number ('{Encoding.UTF8.GetString(_number!)}'): {e.Message}");
            }
        }
    }

    public int Int
    {
        get
        {
            Expect(Kind.Number);
            if (!Utf8Parser.TryParse(_number, out int value, out var consumed) || consumed != _number!.Length)
                throw Error($"must be a whole number, not '{Encoding.UTF8.GetString(_number!)}'.");
            return value;
        }
    }

    public bool Bool => Expect(Kind.Bool)._bool;

    public string String => Expect(Kind.String)._string!;

    /// <summary>Throws if this object, or any object below it, has keys nobody read.</summary>
    public void RejectUnknownKeys()
    {
        if (_kind == Kind.Object)
        {
            foreach (var member in _members!)
            {
                if (!_read!.Contains(member.Key))
                    throw new GameDataException($"{_source}: unknown key '{member.Value.Path}'.");
                member.Value.RejectUnknownKeys();
            }
        }
        else if (_kind == Kind.Array)
        {
            foreach (var item in _items!)
                item.RejectUnknownKeys();
        }
    }
}
