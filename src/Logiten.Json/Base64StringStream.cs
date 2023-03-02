using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class Base64StringStream : Stream
    {
        private static readonly HashSet<char> ValidBase64Characters = new HashSet<char>(
            new[]
            {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
                'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
                'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
                'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
                '8', '9', '+', '/', '='
            });

        private readonly AsyncStreamReader _reader;
        private readonly Action _onComplete;
        private bool _initialized;

        public Base64StringStream(AsyncStreamReader reader, Action onComplete)
        {
            _reader = reader;
            _onComplete = onComplete;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            await InitializeAsync();
            
            if (count == 0) return 0;

            var resultCount = 0;

            while (resultCount < count && resultCount < buffer.Length)
            {
                var peek = await _reader.PeekAsync();
                
                if (peek == -1)
                {
                    throw new InvalidOperationException("Unterminated Base64 string");
                }

                if (peek == '"')
                {
                    await _reader.ReadAsync();
                    _onComplete();
                    break;
                }

                if (ValidBase64Characters.Contains((char)peek) == false)
                    throw new InvalidOperationException(
                        "Invalid character detected in stream");

                buffer[offset] = (byte)await _reader.ReadAsync();
                offset++;
                resultCount++;
            }

            return resultCount;
        }

        private async Task InitializeAsync()
        {
            if (_initialized)
                return;

            var firstChar = await _reader.ReadAsync();
            if (firstChar != '"')
                throw new InvalidOperationException("First character should be: \"");

            _initialized = true;
        }

        
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get; set; }
    }
}