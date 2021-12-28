using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Simple cache interface
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TVal"></typeparam>
    public interface ICache<TKey, TVal>
    {
        /// <summary>
        /// Get value for specified key. If value is not in the cache 
        /// supplied function is used to compute it and then it's 
        /// stored in the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="getDelegate"></param>
        /// <returns></returns>
        TVal Get(TKey key, Func<TKey, TVal> getDelegate);
        /// <summary>
        /// Remove cache entry
        /// </summary>
        /// <param name="k"></param>
        void Remove(TKey k);
        /// <summary>
        /// Number of entries in the cache
        /// </summary>
        int Count { get; }
    }

    /// <summary>
    /// Simple in memory LRU cache with constant cost of item insert and retrieve
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TVal"></typeparam>
    public class SimpleCache<TKey, TVal> : ICache<TKey, TVal>
    {
        //private Dictionary<TKey, TVal>[] _buckets = new Dictionary<TKey, TVal>[5];
        private Bucket[] _buckets = new Bucket[5];
        private Logger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Maximum number of entries the cache can hold
        /// </summary>
        public int MaxCapacity { get; set; }
        

        private class Bucket
        {
            public Bucket()
            {
                Data = new Dictionary<TKey, TVal>();
                RetiredDate = DateTime.Now;
            }

            public new Dictionary<TKey, TVal> Data { get; set; }
            public DateTime RetiredDate { get; set; }
        }
        /// <summary>
        /// Number of staging buckets.
        /// Default is 5 and it's a reasonable value.
        /// </summary>
        public int Buckets 
        {
            get { return _buckets.Length; }
            set
            {
                if (value <= 1 || value >= 1000) throw new Exception("Invalid bucket number");
                _buckets = new Bucket[value];
                for (int i = 0; i < _buckets.Length; i++) _buckets[i] = new Bucket();
            }
        }

        public int Count
        {
            get { return _buckets.Sum(x => x.Data.Count); }
        }

        public SimpleCache()
        {
            MaxCapacity = 10000;
            Buckets = 5;
        }

        private ulong _gets = 0;
        private ulong _hits = 0;
        /// <summary>
        /// Hit ratio in range 0..1
        /// </summary>
        public double HitRatio
        {
            get
            {
                return _gets == 0 ? 0 : ((double)_hits / _gets);
            }
        }

        
        private TVal Get3(TKey k, Func<TKey, TVal> getDelegate)
        {
            TVal v = default(TVal);
            bool b = false;
            
            lock(_buckets)
            {
                _gets++;
                var d = _buckets.FirstOrDefault(x => x.Data.TryGetValue(k, out v));
                if (d != null)
                {
                    if (d != _buckets[0])
                    {
                        d.Data.Remove(k);
                        AddNew(k, v);
                    }
                    _hits++;
                    return v;
                }
            }
            log.Trace("cache miss for {0}", k);
            if (!b)
                v = getDelegate(k);
            
            lock (_buckets)
            {
                TVal v2 = v;
                var d = _buckets.FirstOrDefault(x => x.Data.TryGetValue(k, out v2));
                if (d != null)
                {
                    if (d == _buckets[0]) return v; 
                    d.Data.Remove(k);
                }
                AddNew(k, v);
                return v;
            }
        }

        

        private void AddNew(TKey k, TVal v)
        {
            if (_buckets[0].Data.Count >= (((double) MaxCapacity) / Buckets) )
            {
                //shift buckets
                //and throw away old ones
                var ccnt = Count;
                log.Trace("Shifting buckets, key={0}, Count={1}", k, ccnt);
                var c = _buckets[0];
                _buckets[0].RetiredDate = DateTime.Now;
                
                for (int i = _buckets.Length - 1; i > 0; i--)
                {
                    var b = _buckets[i - 1];
                    if (b.Data.Count > 0 && (ccnt >= MaxCapacity))
                    {
                        log.Trace("Clearing bucket {0} k = {1}", i - 1, k);
                        ccnt -= b.Data.Count;
                        b = new Bucket();
                    }
                    _buckets[i] = b;
                }
                _buckets[0] = new Bucket();
            }
            _buckets[0].Data.Add(k, v);
        }

        public TVal Get(TKey key, Func<TKey, TVal> getDelegate)
        {
            if (MaxCapacity == 0) return getDelegate(key);
            return Get3(key, getDelegate);
        }

        public void Remove(TKey k)
        {
            lock (_buckets)
            {
                var d = _buckets.FirstOrDefault(x => x.Data.ContainsKey(k));
                if (d != null) d.Data.Remove(k);
            }
        }
    }

    public class SimpleTimedCache<TKey, TVal> : ICache<TKey, TVal>
    {
        /// <summary>
        /// Max age of cache entries
        /// </summary>
        public TimeSpan MaxAge { get; set; }

        private class CacheEntry
        {
            public DateTime TimeStamp { get; set; }
            public TVal Value { get; set; }
        }

        private SimpleCache<TKey, CacheEntry> _cache = new SimpleCache<TKey, CacheEntry>();

        public int MaxCapacity
        {
            get { return _cache.MaxCapacity; }
            set { _cache.MaxCapacity = value; }
        }

        public int Buckets
        {
            get { return _cache.Buckets; }
            set { _cache.Buckets = value; }
        }

        public TVal Get(TKey key, Func<TKey, TVal> getDelegate)
        {
            Func<TKey, CacheEntry> f = delegate(TKey k)
            {
                return new CacheEntry { TimeStamp = DateTime.Now, Value = getDelegate(k) };
            };

            var ce = _cache.Get(key, f);
            if (ce.TimeStamp.Add(MaxAge) < DateTime.Now) //update the value in-place 
            {
                ce.TimeStamp = DateTime.Now;
                ce.Value = getDelegate(key);
            }
            return ce.Value;
        }

        public void Remove(TKey k)
        {
            _cache.Remove(k);
        }

        public int Count
        {
            get { return _cache.Count; }
        }
    }
}
