using System;
using System.Collections.Generic;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Fixed-capacity ring buffer. Overwrites oldest entries when full.
    /// </summary>
    public class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Add an item. Overwrites oldest if at capacity.
        /// </summary>
        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        /// <summary>
        /// Return all items in order from oldest to newest.
        /// </summary>
        public List<T> ToList()
        {
            var result = new List<T>(_count);
            if (_count == 0) return result;

            int start = _count < _buffer.Length ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _buffer.Length;
                result.Add(_buffer[idx]);
            }
            return result;
        }

        /// <summary>
        /// Clear all items.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }
}
