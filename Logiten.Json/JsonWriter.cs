using System;
using System.IO;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class JsonWriter
    {
        private readonly StreamWriter _writer;
        private JsonToken _currentToken;

        public JsonWriter(Stream outputStream)
        {
            _writer = new StreamWriter(outputStream);
            _currentToken = JsonToken.ObjectStart;
        }

        private JsonWriter(StreamWriter writer)
        {
            _writer = writer;
            _currentToken = JsonToken.ObjectStart;
        }

        public async Task WriteObjectStartAsync()
        {
            if (_currentToken != JsonToken.ObjectStart)
                throw new InvalidOperationException();

            await _writer.WriteAsync("{ ");
        }

        public async Task WriteInt32Async(string propertyName, int? value)
        {
            await WriteCommaAsync();
            await _writer.WriteAsync(
                $"\"{propertyName}\": {value?.ToString() ?? "null"}");
            _currentToken = JsonToken.NumberValue;
        }

        public async Task WriteStringAsync(string propertyName, string? value)
        {
            await WriteCommaAsync();
            var output = value == null ? "null" : $"\"{value}\"";
            await _writer.WriteAsync($"\"{propertyName}\": {output}");
            _currentToken = JsonToken.StringEnd;
        }

        public async Task WriteStreamAsync(
            string propertyName,
            Stream? value)
        {
            await WriteCommaAsync();

            await _writer.WriteAsync($"\"{propertyName}\": ");

            if (value == null)
            {
                await _writer.WriteAsync("null");
                return;
            }

            await _writer.WriteAsync('"');
            await _writer.FlushAsync();
            var inputStream = new Base64EncodingStream(value);
            var outputStream = _writer.BaseStream;
            await inputStream.CopyToAsync(outputStream);
            await _writer.WriteAsync('"'); 
            
            _currentToken = JsonToken.StringEnd;
        }

        private async Task WriteCommaAsync()
        {
            if (_currentToken == JsonToken.ObjectEnd
                || _currentToken == JsonToken.StringEnd
                || _currentToken == JsonToken.NumberValue)
                await _writer.WriteAsync(", ");
        }

        public async ValueTask DisposeAsync()
        {
            await _writer.FlushAsync();
            await _writer.DisposeAsync();
        }

        public Task WriteObjectEndAsync()
        {
            return _writer.WriteAsync(" }");
        }

        public Task FlushAsync()
        {
            return _writer.FlushAsync();
        }

        public async Task<JsonWriter> WriteObjectAsync(string propertyName)
        {
            await WriteCommaAsync();
            await _writer.WriteAsync($"\"{propertyName}\": ");
            return new JsonWriter(_writer);
        }
    }
}