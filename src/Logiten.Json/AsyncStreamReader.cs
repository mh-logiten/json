using System.IO;
using System.Threading.Tasks;

namespace Logiten.Json
{
    public class AsyncStreamReader
    {
        private readonly StreamReader _reader;
        private readonly char[] _buffer = new char[1];
        private int _nextChar;
        private bool _peeked;
        
        public AsyncStreamReader(StreamReader reader)
        {
            _reader = reader;
        }

        public StreamReader BaseReader => _reader;
        
        public bool EndOfStream { get; private set; }

        public async Task<int> ReadAsync()
        {
            if (_peeked)
            {
                _peeked = false;
                EndOfStream = _nextChar == -1;
                return _nextChar;
            }

            var currentCount = await _reader.ReadAsync(_buffer, 0, _buffer.Length);
            if (currentCount == 0)
            {
                _nextChar = -1;
                EndOfStream = true;
                return -1;
            }
            _nextChar = _buffer[0];
            return _nextChar;
        }

        public async Task<int> PeekAsync()
        {
            if (_peeked == false)
            {
                await ReadAsync();
                _peeked = true;
            }
            
            return _nextChar;
        }
    }
}