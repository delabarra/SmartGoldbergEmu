using System;
using System.Globalization;
using System.Text;

namespace SmartGoldbergEmu.JsonKit
{
    internal sealed class JsonParser
    {
        private readonly string _text;
        private int _index;

        public JsonParser(string text)
        {
            _text = text ?? string.Empty;
            _index = 0;
        }

        public JsonValue ParseValue()
        {
            SkipWhitespace();
            if (_index >= _text.Length)
                throw new JsonReaderException("Unexpected end of JSON input.");

            char c = _text[_index];
            switch (c)
            {
                case '{':
                    return ParseObject();
                case '[':
                    return ParseArray();
                case '"':
                    return new JsonString(ParseString());
                case 't':
                    ExpectLiteral("true");
                    return new JsonBool(true);
                case 'f':
                    ExpectLiteral("false");
                    return new JsonBool(false);
                case 'n':
                    ExpectLiteral("null");
                    return JsonNull.Instance;
                default:
                    if (c == '-' || char.IsDigit(c))
                        return ParseNumber();
                    throw new JsonReaderException("Invalid JSON at position " + _index + ".");
            }
        }

        private JsonObject ParseObject()
        {
            Expect('{');
            var obj = new JsonObject();
            SkipWhitespace();
            if (TryConsume('}'))
                return obj;

            while (true)
            {
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != '"')
                    throw new JsonReaderException("Expected property name in JSON object.");

                string name = ParseString();
                SkipWhitespace();
                Expect(':');
                obj[name] = ParseValue();
                SkipWhitespace();
                if (TryConsume('}'))
                    return obj;
                Expect(',');
            }
        }

        private JsonArray ParseArray()
        {
            Expect('[');
            var array = new JsonArray();
            SkipWhitespace();
            if (TryConsume(']'))
                return array;

            while (true)
            {
                array.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                    return array;
                Expect(',');
                SkipWhitespace();
            }
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (_index < _text.Length)
            {
                char c = _text[_index++];
                if (c == '"')
                    return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (_index >= _text.Length)
                    throw new JsonReaderException("Unterminated string escape.");

                char esc = _text[_index++];
                switch (esc)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(esc);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        sb.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw new JsonReaderException("Invalid escape sequence in JSON string.");
                }
            }

            throw new JsonReaderException("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_index + 4 > _text.Length)
                throw new JsonReaderException("Invalid \\u escape in JSON string.");

            int code = 0;
            for (int i = 0; i < 4; i++)
            {
                char hex = _text[_index++];
                int digit;
                if (hex >= '0' && hex <= '9')
                    digit = hex - '0';
                else if (hex >= 'a' && hex <= 'f')
                    digit = hex - 'a' + 10;
                else if (hex >= 'A' && hex <= 'F')
                    digit = hex - 'A' + 10;
                else
                    throw new JsonReaderException("Invalid \\u escape in JSON string.");

                code = (code << 4) + digit;
            }

            return (char)code;
        }

        private JsonNumber ParseNumber()
        {
            int start = _index;
            if (_text[_index] == '-')
                _index++;

            if (_index >= _text.Length)
                throw new JsonReaderException("Invalid JSON number.");

            if (_text[_index] == '0')
            {
                _index++;
            }
            else
            {
                if (!char.IsDigit(_text[_index]))
                    throw new JsonReaderException("Invalid JSON number.");
                while (_index < _text.Length && char.IsDigit(_text[_index]))
                    _index++;
            }

            bool isFloat = false;
            if (_index < _text.Length && _text[_index] == '.')
            {
                isFloat = true;
                _index++;
                if (_index >= _text.Length || !char.IsDigit(_text[_index]))
                    throw new JsonReaderException("Invalid JSON number.");
                while (_index < _text.Length && char.IsDigit(_text[_index]))
                    _index++;
            }

            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                isFloat = true;
                _index++;
                if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-'))
                    _index++;
                if (_index >= _text.Length || !char.IsDigit(_text[_index]))
                    throw new JsonReaderException("Invalid JSON number.");
                while (_index < _text.Length && char.IsDigit(_text[_index]))
                    _index++;
            }

            string slice = _text.Substring(start, _index - start);
            if (isFloat)
            {
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw new JsonReaderException("Invalid JSON number.");
                return new JsonNumber(d);
            }

            if (!long.TryParse(slice, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                throw new JsonReaderException("Invalid JSON number.");
            return new JsonNumber(l);
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length)
            {
                char c = _text[_index];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    _index++;
                else
                    break;
            }
        }

        private void Expect(char expected)
        {
            if (_index >= _text.Length || _text[_index] != expected)
                throw new JsonReaderException("Invalid JSON at position " + _index + ".");
            _index++;
        }

        private bool TryConsume(char expected)
        {
            if (_index < _text.Length && _text[_index] == expected)
            {
                _index++;
                return true;
            }
            return false;
        }

        private void ExpectLiteral(string literal)
        {
            if (_text.Length - _index < literal.Length ||
                string.Compare(_text, _index, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
            {
                throw new JsonReaderException("Invalid JSON literal at position " + _index + ".");
            }
            _index += literal.Length;
        }
    }
}
