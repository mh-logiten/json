using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class JsonReader
    {
        private readonly AsyncStreamReader _reader;
        private int? _currentNumber;
        private string? _currentString;
        private JsonToken _currentToken = JsonToken.Unknown;
        private bool _childObjectIsOpen;

        public JsonReader(Stream inputStream)
        {
            var streamReader = new StreamReader(inputStream, Encoding.UTF8);
            _reader = new AsyncStreamReader(streamReader);
        }

        private JsonReader(JsonReader parent)
        {
            _reader = parent._reader;
        }
        
        public async Task<int?> ReadInt32Async(string propertyName)
        {
            await MoveToPropertyAsync(propertyName);
            if (await ReadAsync()
                && (_currentToken == JsonToken.NumberValue
                    || _currentToken == JsonToken.NullValue))
            {
                return _currentNumber;
            }

            throw new InvalidOperationException("Expected number value");
        }

        public async Task<string?> ReadStringAsync(string propertyName)
        {
            await MoveToPropertyAsync(propertyName);

            if (await ReadAsync())
            {
                switch (_currentToken)
                {
                    case JsonToken.StringStart:
                        _currentToken = JsonToken.StringEnd;
                        return await ReadStringAsync();
                    case JsonToken.NullValue:
                        return null;
                }
            }
      
            throw new InvalidOperationException("Expected string or null");
        }

        public async Task<Stream?> ReadStreamAsync(string propertyName)
        {
            await MoveToPropertyAsync(propertyName);

            if (!await ReadAsync())
                throw new InvalidOperationException();
            
            var result = _currentToken switch
            {
                JsonToken.StringStart =>
                    new Base64DecodingStream(
                        new Base64StringStream(
                            _reader,
                            onComplete: () => _currentToken = JsonToken.StringEnd)),
                JsonToken.NullValue =>
                    null,
                _ => throw new InvalidOperationException("Expected string or null")
            };

            return result;
        }
        
        public async Task<JsonReader> ReadObjectAsync(string propertyName)
        {
            await MoveToPropertyAsync(propertyName);
            await ConsumeWhitespaceAsync();
            await ConsumeOrThrowAsync(':');
            await ConsumeWhitespaceAsync();
            
            if (await _reader.PeekAsync() != '{')
                throw new InvalidOperationException();

            return await ReadObjectAsync();
        }
        
        private Task<JsonReader> ReadObjectAsync()
        {
            return Task.FromResult(new JsonReader(this));
        }
        
        private async Task<int> ReadInt32Async()
        {
            var builder = new StringBuilder();
            while (_reader.EndOfStream == false)
            {
                var ch = await _reader.PeekAsync();
                if (ch == -1 || Char.IsNumber((char)ch) == false)
                    break;
                builder.Append((char)await _reader.ReadAsync());
            }
            return int.Parse(builder.ToString());
        }
        
        private async Task MoveToPropertyAsync(string propertyName)
        {
            while (await ReadAsync())
            {
                if (_currentToken == JsonToken.PropertyName
                    && _currentString == propertyName)
                    return;
            }
            
            throw new InvalidOperationException(
                $"Property {propertyName} not found. "
                + "Properties must be requested in the same order as they appear in the "
                + "input stream");
        }
        
        private async Task<bool> ReadAsync()
        {
            if (_currentToken == JsonToken.StringStart)
                throw new NotSupportedException(
                    "Use one of the ReadString or ReadStream methods");
            
            _currentNumber = null;
            _currentString = null;
            
            await ConsumeWhitespaceAsync();
            
            if (_reader.EndOfStream)
                return false;
            
            var ch = await _reader.PeekAsync();

            if (ch == -1)
                return false;
            
            if (_currentToken == JsonToken.NullValue
                || _currentToken == JsonToken.NumberValue
                || _currentToken == JsonToken.StringEnd)
            {
                if (ch == ',')
                    await _reader.ReadAsync();
                await ConsumeWhitespaceAsync();
                if (_reader.EndOfStream)
                    throw new InvalidOperationException("end of stream");
                ch = await _reader.PeekAsync();
            }

            switch (_currentToken)
            {
                case JsonToken.Unknown:
                    if (ch != '{')
                        throw new InvalidOperationException("unknown");
                    _currentToken = JsonToken.ObjectStart;
                    await _reader.ReadAsync();
                    break;

                case JsonToken.ObjectStart:
                case JsonToken.StringEnd:
                case JsonToken.NumberValue:
                case JsonToken.NullValue:
                case JsonToken.ObjectEnd:
                    switch (ch)
                    {
                        case '}':
                            _currentToken = JsonToken.ObjectEnd;
                            await _reader.ReadAsync();
                            if (_childObjectIsOpen)
                                _childObjectIsOpen = false;
                            break;
                        case '"':
                            _currentToken = JsonToken.PropertyName;
                            _currentString = await ReadStringAsync();
                            break;
                        default:
                            throw new InvalidOperationException($"unexpected char {ch}");
                    }
                    break;
                    
                case JsonToken.PropertyName:
                    await ConsumeOrThrowAsync(':');
                    await ConsumeWhitespaceAsync();
                    ch = await _reader.PeekAsync();
                    if (ch != -1 && Char.IsNumber((char)ch))
                    {
                        _currentToken = JsonToken.NumberValue;
                        _currentNumber = await ReadInt32Async();
                    }
                    else
                    {
                        _currentToken = ch switch
                        {
                            '"' => JsonToken.StringStart,
                            '{' => JsonToken.ObjectStart,
                            'n' =>
                                await ReadNullAsync()
                                    ? JsonToken.NullValue
                                    : throw new InvalidOperationException("Expected null"),
                            _ => throw new InvalidOperationException()
                        };

                        if (_currentToken == JsonToken.ObjectStart)
                            await _reader.ReadAsync();
                    }
                    break;
                
                default:
                    throw new NotSupportedException("Unsupported token type");
            }

            return true;
        }
        
        private async Task ConsumeOrThrowAsync(char ch)
        {
            if (_reader.EndOfStream
                || await _reader.PeekAsync() != ch)
                throw new InvalidOperationException();

            await _reader.ReadAsync();
        }

        private async Task ConsumeWhitespaceAsync()
        {
            while (_reader.EndOfStream == false
                   && await _reader.PeekAsync() != -1
                   && Char.IsWhiteSpace((char)await _reader.PeekAsync()))
                await _reader.ReadAsync();
        }

        private async Task<string> ReadStringAsync()
        {
            var chars = new List<char>();
            var escape = false;
      
            chars.Add((char)await _reader.ReadAsync());
            
            while (_reader.EndOfStream == false)
            {
                var ch = await _reader.ReadAsync();

                if (_reader.EndOfStream && (escape || ch != '"'))
                    throw new InvalidOperationException("Unterminated string detected");
                
                chars.Add((char)ch);
                
                if (escape)
                {
                    escape = false;
                }
                else
                {
                    if (ch == '\\') escape = true;
                    else if (ch == '"') break;
                }
            }
            
            var result =
                (string)JsonSerializer.Deserialize(
                    new string(chars.ToArray()),
                    typeof(string));

            if (result == null)
                throw new InvalidOperationException("Unable to deserialize string");
            
            return result;
        }

        private async Task<bool> ReadNullAsync()
        {
            var chars = "null";
            foreach (var nullChar in chars)
            {
                if (_reader.EndOfStream)
                    return false;

                if (nullChar != await _reader.ReadAsync())
                    return false;
            }

            if (_reader.EndOfStream)
                return false;

            var peek = await _reader.PeekAsync();

            if (peek == -1)
                return false;
            
            if (Char.IsWhiteSpace((char)peek) == false
                && peek != '}'
                && peek != ',')
                return false;

            return true;
        }        
    }
}