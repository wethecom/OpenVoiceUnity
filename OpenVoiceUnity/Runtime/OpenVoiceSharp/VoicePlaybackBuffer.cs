using System;

namespace OpenVoiceSharp
{
    /// <summary>
    /// Thread-safe PCM16 playback buffer.
    /// Supports enqueueing decoded PCM bytes and reading a fixed byte count with zero-fill.
    /// </summary>
    public sealed class VoicePlaybackBuffer
    {
        private readonly object _sync = new();
        private byte[] _buffer;
        private int _readIndex;
        private int _writeIndex;
        private int _count;

        public int AvailableBytes
        {
            get
            {
                lock (_sync)
                    return _count;
            }
        }

        public VoicePlaybackBuffer(int initialCapacity = 16384)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _buffer = new byte[initialCapacity];
        }

        public void Enqueue(byte[] source, int length, int sourceOffset = 0)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (length < 0 || sourceOffset < 0 || sourceOffset + length > source.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return;

            lock (_sync)
            {
                EnsureCapacityFor(length);

                int first = Math.Min(length, _buffer.Length - _writeIndex);
                Buffer.BlockCopy(source, sourceOffset, _buffer, _writeIndex, first);
                int remaining = length - first;
                if (remaining > 0)
                    Buffer.BlockCopy(source, sourceOffset + first, _buffer, 0, remaining);

                _writeIndex = (_writeIndex + length) % _buffer.Length;
                _count += length;
            }
        }

        public int ReadAndFillSilence(byte[] destination, int requestedBytes, int destinationOffset = 0)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (requestedBytes < 0 || destinationOffset < 0 || destinationOffset + requestedBytes > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(requestedBytes));
            if (requestedBytes == 0)
                return 0;

            int copied;
            lock (_sync)
            {
                copied = Math.Min(requestedBytes, _count);
                if (copied > 0)
                {
                    int first = Math.Min(copied, _buffer.Length - _readIndex);
                    Buffer.BlockCopy(_buffer, _readIndex, destination, destinationOffset, first);
                    int remaining = copied - first;
                    if (remaining > 0)
                        Buffer.BlockCopy(_buffer, 0, destination, destinationOffset + first, remaining);

                    _readIndex = (_readIndex + copied) % _buffer.Length;
                    _count -= copied;
                }
            }

            if (copied < requestedBytes)
                Array.Clear(destination, destinationOffset + copied, requestedBytes - copied);

            return copied;
        }

        /// <summary>
        /// Reads up to <paramref name="requestedBytes"/> available bytes without silence fill.
        /// Returns the number of bytes copied.
        /// </summary>
        public int ReadAvailable(byte[] destination, int requestedBytes, int destinationOffset = 0)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (requestedBytes < 0 || destinationOffset < 0 || destinationOffset + requestedBytes > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(requestedBytes));
            if (requestedBytes == 0)
                return 0;

            int copied;
            lock (_sync)
            {
                copied = Math.Min(requestedBytes, _count);
                if (copied <= 0)
                    return 0;

                int first = Math.Min(copied, _buffer.Length - _readIndex);
                Buffer.BlockCopy(_buffer, _readIndex, destination, destinationOffset, first);
                int remaining = copied - first;
                if (remaining > 0)
                    Buffer.BlockCopy(_buffer, 0, destination, destinationOffset + first, remaining);

                _readIndex = (_readIndex + copied) % _buffer.Length;
                _count -= copied;
            }

            return copied;
        }

        public void Flush()
        {
            lock (_sync)
            {
                _readIndex = 0;
                _writeIndex = 0;
                _count = 0;
            }
        }

        private void EnsureCapacityFor(int additionalBytes)
        {
            int required = _count + additionalBytes;
            if (required <= _buffer.Length)
                return;

            int newCapacity = _buffer.Length;
            while (newCapacity < required)
                newCapacity *= 2;

            byte[] newBuffer = new byte[newCapacity];
            if (_count > 0)
            {
                if (_readIndex < _writeIndex)
                {
                    Buffer.BlockCopy(_buffer, _readIndex, newBuffer, 0, _count);
                }
                else
                {
                    int first = _buffer.Length - _readIndex;
                    Buffer.BlockCopy(_buffer, _readIndex, newBuffer, 0, first);
                    Buffer.BlockCopy(_buffer, 0, newBuffer, first, _writeIndex);
                }
            }

            _buffer = newBuffer;
            _readIndex = 0;
            _writeIndex = _count;
        }
    }
}
