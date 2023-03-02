using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class Base64DecodingStream : Stream
    {
        private readonly Stream _inputStream;
        private readonly FromBase64Transform _readTransform = new FromBase64Transform();
        private readonly Queue<byte> _overflow = new Queue<byte>();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public Base64DecodingStream(Stream inputStream)
        {
            _inputStream = inputStream;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            if (count == 0) return 0;

            var inputBuffer = new byte[_readTransform.InputBlockSize];
            var outputBuffer = new byte[_readTransform.OutputBlockSize];
            var resultCount = 0;

            while (resultCount < count
                   && _overflow.TryDequeue(out var overflowByte))
            {
                buffer[offset] = overflowByte;
                offset++;
                resultCount++;
            }

            while (resultCount < count)
            {
                var encodedCount = await _inputStream.ReadAsync(
                    inputBuffer,
                    0,
                    inputBuffer.Length,
                    cancellationToken);

                if (encodedCount == 0)
                    break;

                if (encodedCount != inputBuffer.Length)
                    throw new InvalidOperationException(
                        "Invalid Base64 stream, invalid number of characters, "
                        + $"must be a multiple of {inputBuffer.Length}");

                var decodedCount = _readTransform.TransformBlock(
                    inputBuffer,
                    0,
                    inputBuffer.Length,
                    outputBuffer,
                    0);

                for (int i = 0; i < decodedCount; i++)
                {
                    if (resultCount < count)
                    {
                        buffer[offset] = outputBuffer[i];
                        offset++;
                        resultCount++;
                    }
                    else
                    {
                        _overflow.Enqueue(outputBuffer[i]);
                    }
                }

                // reached the final block with padding (==)
                if (decodedCount < outputBuffer.Length)
                    break;
            }

            return resultCount;
        }

        public override void Flush()
        {
        }

        #region Unsupported overrides

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion
    }
}