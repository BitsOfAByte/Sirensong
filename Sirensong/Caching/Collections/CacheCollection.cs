using System;
using System.Collections;
using System.Collections.Generic;

namespace Sirensong.Caching.Collections
{
    public class CacheCollection<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable where TKey : notnull where TValue : notnull
    {
        private bool disposedValue;

        /// <summary>
        /// The dictionary of values for the cache.
        /// </summary>
        private readonly Dictionary<TKey, TValue> cache = new();

#pragma warning disable IDE0052, IDE0051

        /// <summary>
        /// The dictionary of Keys to <see cref="KeyExpiryInfo"/>.
        /// </summary>
        private readonly Dictionary<TKey, KeyExpiryInfo> accessTimes = new();

        /// <summary>
        /// The <see cref="CacheOptions{TKey, TValue}"/> for this cache.
        /// </summary>
        private readonly CacheOptions<TKey, TValue> options;

        /// <summary>
        /// Creates a new <see cref="CacheCollection{TKey,TValue}" /> with default options.
        /// </summary>
        public CacheCollection() : this(new CacheOptions<TKey, TValue>())
        {

        }

        /// <summary>
        /// Creates a new <see cref="CacheCollection{TKey,TValue}" /> with the specified options.
        /// </summary>
        /// <param name="options">The <see cref="CacheOptions{TKey, TValue}"/> for this cache.</param>
        public CacheCollection(CacheOptions<TKey, TValue> options) => this.options = options;

        /// <summary>
        /// Disposes of the cache and all its values.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposedValue)
            {
                this.RemoveAllKeys();

                GC.SuppressFinalize(this);

                this.disposedValue = true;
            }
        }

        /// <summary>
        /// Gets the given key from the cache.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>The value for the given key.</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public TValue this[TKey key]
        {
            get
            {
                if (this.cache.TryGetValue(key, out var value))
                {
                    this.accessTimes[key].Accessed();
                    return value;
                }

                throw new KeyNotFoundException();
            }
        }

        /// <summary>
        /// Removes all keys from the cache.
        /// </summary>
        private void RemoveAllKeys()
        {
            foreach (var key in this.cache.Keys)
            {
                this.options.OnExpiry?.Invoke(key, this.cache[key]);
            }

            this.cache.Clear();
            this.accessTimes.Clear();
        }


        /// <summary>
        /// Checks to see if the given key has expired.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key has expired, false otherwise.</returns>
        public bool IsExpired(TKey key)
        {
            if (!this.cache.ContainsKey(key) || !this.accessTimes.ContainsKey(key))
            {
                return false;
            }

            if (this.options.AbsoluteExpiry.HasValue)
            {
                if (DateTime.Now - this.accessTimes[key].LastUpdateTime > this.options.AbsoluteExpiry)
                {
                    return true;
                }
            }

            if (this.options.SlidingExpiry.HasValue)
            {
                if (DateTime.Now - this.accessTimes[key].LastAccessTime > this.options.SlidingExpiry)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Expires the given key.
        /// </summary>
        /// <param name="key"></param>
        private void Expire(TKey key)
        {
            this.cache.Remove(key);
            this.accessTimes.Remove(key);
            this.options.OnExpiry?.Invoke(key, this.cache[key]);
        }

        /// <summary>
        /// Gets a value or adds it to the cache.
        /// </summary>
        /// <param name="key">The key to get or add.</param>
        /// <param name="valueFactory">The factory to create the value if it doesn't exist.</param>
        /// <returns>The value for the given key.</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (this.cache.TryGetValue(key, out var value))
            {
                if (this.IsExpired(key))
                {
                    this.Expire(key);
                }
                else
                {
                    this.accessTimes[key].Accessed();
                    return value;
                }
            }

            value = valueFactory(key);
            this.cache.Add(key, value);
            return value;
        }

        /// <summary>
        /// Adds or updates a value in the cache.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="valueFactory">The factory to create/update the value.</param>
        public void AddOrUpdate(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (this.cache.TryGetValue(key, out _))
            {
                if (this.IsExpired(key))
                {
                    this.Expire(key);
                }
                else
                {
                    this.accessTimes[key].Updated();
                    this.cache[key] = valueFactory(key);
                    return;
                }
            }

            var value = valueFactory(key);
            this.cache.Add(key, value);
        }

        /// <summary>
        /// Removes a key from the cache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>True if the key was removed, false otherwise.</returns>
        public bool Remove(TKey key)
        {
            if (this.cache.Remove(key))
            {
                this.accessTimes.Remove(key);
                return true;
            }

            return false;
        }

        /// <summary>
        /// All the keys in the cache.
        /// </summary>
        public IReadOnlyCollection<TKey> Keys => this.cache.Keys;

        /// <summary>
        /// All the values in the cache.
        /// </summary>
        public IReadOnlyCollection<TValue> Values => this.cache.Values;

        /// <summary>
        /// Gets the enumerator for the cache.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.cache.GetEnumerator();

        /// <summary>
        /// Gets the enumerator for the cache.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator() => this.cache.GetEnumerator();

        /// <summary>
        /// Represents expiry information for a key.
        /// </summary>
        private struct KeyExpiryInfo
        {
            /// <summary>
            /// The last time the key was accessed.
            /// </summary>
            public DateTime LastAccessTime { get; private set; } = DateTime.Now;

            /// <summary>
            /// The last time the key was updated.
            /// </summary>
            public DateTime LastUpdateTime { get; private set; } = DateTime.Now;

            /// <summary>
            /// Creates a new instance of <see cref="KeyExpiryInfo"/> with the current time.
            /// </summary>
            public KeyExpiryInfo()
            {

            }

            /// <summary>
            /// Creates a new instance of <see cref="KeyExpiryInfo"/> with the given times.
            /// </summary>
            /// <param name="lastAccessTime"></param>
            /// <param name="lastUpdateTime"></param>
            public KeyExpiryInfo(DateTime lastAccessTime, DateTime lastUpdateTime)
            {
                this.LastAccessTime = lastAccessTime;
                this.LastUpdateTime = lastUpdateTime;
            }

            /// <summary>
            /// Updates the last access time to the current time.
            /// </summary>
            public void Accessed() => this.LastAccessTime = DateTime.Now;

            /// <summary>
            /// Updates the last update time to the current time.
            /// </summary>
            public void Updated()
            {
                this.Accessed();
                this.LastUpdateTime = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// Represents options for a timed cache.
    /// </summary>
    public struct CacheOptions<TKey, TValue>
    {
        /// <summary>
        /// The sliding expiry time for the cache. Items in the cache will expire after this amount of time since they were last accessed.
        /// Default: null.
        /// </summary>
        public TimeSpan? SlidingExpiry { get; set; } = null;

        /// <summary>
        /// The absolute expiry time for the cache. Items in the cache will expire after this amount of time since they were last updated.
        /// Default: 1 hour.
        /// </summary>
        public TimeSpan? AbsoluteExpiry { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        ///Called when an item is expired from the cache.
        ///Default: null.
        /// </summary>
        public Action<TKey, TValue>? OnExpiry { get; set; }

        /// <summary>
        /// The interval to check for expired items, only used if <see cref="UseBuiltInExpire"/> is true.
        /// Defaults to 1 minute.
        /// </summary>
        public TimeSpan? ExpireInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Creates a new instance of <see cref="CacheOptions{TKey, TValue}"/>.
        /// </summary>
        public CacheOptions()
        {

        }
    }
}
