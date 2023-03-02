using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class Base64EncodingStream : Stream
    {
        private readonly Stream _inputStream;
        private readonly Queue<byte> _overflow = new Queue<byte>();
        private readonly ToBase64Transform _transform = new ToBase64Transform();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        
        public Base64EncodingStream(Stream inputStream)
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
         
            var inputBuffer = new byte[3];
            var outputBuffer = new byte[4];
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
                var byteCount =
                    await _inputStream.ReadAsync(
                        inputBuffer,
                        0,
                        inputBuffer.Length,
                        cancellationToken);

                int transformedCount;
                byte[] transformedBlock;
             
                if (byteCount < inputBuffer.Length)
                {
                    transformedBlock =
                        _transform.TransformFinalBlock(inputBuffer, 0, byteCount);
                    transformedCount = transformedBlock.Length;
                }
                else
                {
                    transformedBlock = outputBuffer;
                    transformedCount = _transform.TransformBlock(
                        inputBuffer,
                        0,
                        byteCount,
                        outputBuffer,
                        0);
                }
             
                for (int i = 0; i < transformedCount; i++)
                {
                    if (resultCount < count)
                    {
                        buffer[offset] = transformedBlock[i];
                        offset++;
                        resultCount++;
                    }
                    else
                    {
                        _overflow.Enqueue(transformedBlock[i]);
                    }
                }
             
                // reached the final block with padding (==)
                if (transformedCount < outputBuffer.Length)
                    break;
            }

            return resultCount;
        }

        public override void Flush()
        {
        }

        #region Unsupported Overrides
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