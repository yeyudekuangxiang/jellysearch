using System;
using System.Collections.Concurrent;
using System.Threading;

namespace JellySearch.Helpers;

public class CacheItem<T>
{
    public T Value { get; set; }
    public DateTime ExpiryTime { get; set; }
    public bool IsExpired => DateTime.Now >= ExpiryTime;
    public string UserId { get; set; }
}

public class StringCache<T>
{
    private readonly ConcurrentDictionary<string, CacheItem<T>> _cache;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval;

    public StringCache() : this(TimeSpan.FromMinutes(5))
    {
    }

    public StringCache(TimeSpan cleanupInterval)
    {
        _cache = new ConcurrentDictionary<string, CacheItem<T>>();
        _cleanupInterval = cleanupInterval;
        _cleanupTimer = new Timer(CleanupExpiredItems, null, _cleanupInterval, _cleanupInterval);
    }

    /// <summary>
    /// 添加或更新缓存项
    /// </summary>
    /// <param name="id">缓存ID</param>
    /// <param name="value">缓存值</param>
    /// <param name="expiry">过期时间</param>
    public void Set(string id,string userId, T value, TimeSpan expiry)
    {
        var cacheItem = new CacheItem<T>
        {
            Value = value,
            ExpiryTime = DateTime.Now.Add(expiry)
            UserId = userId
        };
        
        _cache.AddOrUpdate(id, cacheItem, (key, oldValue) => cacheItem);
    }

    /// <summary>
    /// 获取缓存项
    /// </summary>
    /// <param name="id">缓存ID</param>
    /// <returns>缓存值，如果不存在或已过期则返回default(T)</returns>
    public T Get(string id)
    {
        if (_cache.TryGetValue(id, out var cacheItem))
        {
            if (!cacheItem.IsExpired)
            {
                return cacheItem.Value;
            }
            
            // 如果已过期，移除该项
            _cache.TryRemove(id, out _);
        }
        
        return default(T);
    }

    /// <summary>
    /// 尝试获取缓存项
    /// </summary>
    /// <param name="id">缓存ID</param>
    /// <param name="value">缓存值</param>
    /// <returns>是否成功获取</returns>
    public bool TryGet(string id, out T value)
    {
        value = Get(id);
        return value != null && !value.Equals(default(T));
    }

    /// <summary>
    /// 检查缓存项是否存在且未过期
    /// </summary>
    /// <param name="id">缓存ID</param>
    /// <returns>是否存在</returns>
    public bool Contains(string id)
    {
        return _cache.ContainsKey(id) && !_cache[id].IsExpired;
    }

    /// <summary>
    /// 删除缓存项
    /// </summary>
    /// <param name="id">缓存ID</param>
    /// <returns>是否成功删除</returns>
    public bool Remove(string id)
    {
        return _cache.TryRemove(id, out _);
    }
    public bool RemoveByUserId(string userId)
    {
        var keysToRemove = _cache.Where(kvp => kvp.Value.UserId == userId)
                                 .Select(kvp => kvp.Key)
                                 .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        return keysToRemove.Count > 0;
    }

    /// <summary>
    /// 获取所有缓存键
    /// </summary>
    /// <returns>所有缓存键数组</returns>
    public string[] GetAllKeys()
    {
        return _cache.Keys.ToArray();
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取缓存数量
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// 清理过期项目
    /// </summary>
    private void CleanupExpiredItems(object state)
    {
        var expiredKeys = _cache.Where(kvp => kvp.Value.IsExpired)
                               .Select(kvp => kvp.Key)
                               .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

public class MD5Helper
{
    public static string GenerateMD5(string input)
    {
        using (var md5 = MD5.Create())
        {
            var result = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(result).Replace("-", "").ToLower();
        }
    }
}