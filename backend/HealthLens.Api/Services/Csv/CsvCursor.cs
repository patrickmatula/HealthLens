using System.Buffers;
using System.Globalization;
using System.Text;

namespace HealthLens.Api.Services.Csv;

/// <summary>
/// Forward-only reader for the Takeout time-series CSVs. The whole file is pulled into one pooled
/// char buffer and every field is handed out as a <see cref="ReadOnlySpan{T}"/> slice of it, so
/// parsing the millions of intraday rows in an export never allocates a string per field the way
/// CsvHelper's <c>GetField</c> does. Quotes are honoured for delimiting only (a doubled <c>""</c>
/// inside a quoted field is not unescaped) — the files this reads are plain timestamp/number
/// columns; anything with genuinely structured text stays on CsvHelper.
/// </summary>
public sealed class CsvCursor : IDisposable
{
    private char[] _buffer;
    private readonly int _length;
    private readonly string[] _header;
    private (int Start, int Length)[] _fields = new (int, int)[16];
    private int _fieldCount;
    private int _pos;

    private CsvCursor(char[] buffer, int length)
    {
        _buffer = buffer;
        _length = length;

        if (!NextRow())
        {
            _header = [];
            return;
        }

        _header = new string[_fieldCount];
        for (var i = 0; i < _fieldCount; i++)
        {
            _header[i] = Field(i).Trim().ToString();
        }
    }

    /// <summary>Opens <paramref name="path"/> and consumes its header row, or returns null if the file doesn't exist.</summary>
    public static CsvCursor? Open(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var buffer = ArrayPool<char>.Shared.Rent((int)Math.Clamp(stream.Length + 1, 4096, int.MaxValue));
        var length = 0;
        while (true)
        {
            if (length == buffer.Length)
            {
                var grown = ArrayPool<char>.Shared.Rent(buffer.Length * 2);
                buffer.AsSpan(0, length).CopyTo(grown);
                ArrayPool<char>.Shared.Return(buffer);
                buffer = grown;
            }

            var read = reader.Read(buffer, length, buffer.Length - length);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return new CsvCursor(buffer, length);
    }

    /// <summary>Index of the named column, or -1 when the export doesn't carry it.</summary>
    public int Column(string name)
    {
        for (var i = 0; i < _header.Length; i++)
        {
            if (string.Equals(_header[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Index of the first column that isn't one of <paramref name="excluded"/> — these exports name the value column after the metric.</summary>
    public int ValueColumn(params ReadOnlySpan<string> excluded)
    {
        for (var i = 0; i < _header.Length; i++)
        {
            if (!excluded.Contains(_header[i], StringComparer.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public bool NextRow()
    {
        var span = _buffer.AsSpan(0, _length);
        while (_pos < _length && span[_pos] is '\r' or '\n')
        {
            _pos++;
        }

        if (_pos >= _length)
        {
            return false;
        }

        _fieldCount = 0;
        var start = _pos;
        var quoted = false;
        var i = _pos;

        for (; i < _length; i++)
        {
            var c = span[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < _length && span[i + 1] == '"')
                    {
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }

                continue;
            }

            if (c == '"' && i == start)
            {
                quoted = true;
            }
            else if (c == ',')
            {
                AddField(start, i - start);
                start = i + 1;
            }
            else if (c is '\r' or '\n')
            {
                break;
            }
        }

        AddField(start, i - start);
        _pos = i;
        return true;
    }

    public ReadOnlySpan<char> Field(int index) =>
        (uint)index < (uint)_fieldCount ? _buffer.AsSpan(_fields[index].Start, _fields[index].Length) : default;

    public bool TryGetDouble(int index, out double value) =>
        double.TryParse(Field(index), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public double? GetDouble(int index) => TryGetDouble(index, out var value) ? value : null;

    public double GetDoubleOrZero(int index) => TryGetDouble(index, out var value) ? value : 0;

    public int GetInt32OrZero(int index) => int.TryParse(Field(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public long? GetInt64(int index) => long.TryParse(Field(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    public bool? GetBool(int index) => bool.TryParse(Field(index), out var value) ? value : null;

    public string GetString(int index) => Field(index).ToString();

    public DateTime? GetUtc(int index) => Timestamps.TryParseUtc(Field(index), out var value) ? value : null;

    public DateOnly? GetUtcDate(int index) => Timestamps.TryParseUtc(Field(index), out var value) ? DateOnly.FromDateTime(value) : null;

    public void Dispose()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = [];
        }
    }

    private void AddField(int start, int length)
    {
        if (length >= 2 && _buffer[start] == '"' && _buffer[start + length - 1] == '"')
        {
            start++;
            length -= 2;
        }

        if (_fieldCount == _fields.Length)
        {
            Array.Resize(ref _fields, _fields.Length * 2);
        }

        _fields[_fieldCount++] = (start, length);
    }
}
