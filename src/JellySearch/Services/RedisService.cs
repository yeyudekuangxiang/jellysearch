using StackExchange.Redis;
using JellySearch.Jellyfin;
using System.Text.Json;

namespace JellySearch.Services;
public class RedisService: IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly int _defaultDb;
    private const string KeyPrefix = "jellyfin:item:";

    // localhost:6379,password=yourpassword,defaultDatabase=0
    private string? RedisUrl { get; set; } = Environment.GetEnvironmentVariable("REDIS_URL");
    private string? UseCache { get; set; } = Environment.GetEnvironmentVariable("USE_CACHE");
    public RedisService(IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(RedisUrl))
        {
            return;
        }


        // 创建Redis连接
        _redis = ConnectionMultiplexer.Connect(RedisUrl);
        _db = _redis.GetDatabase(_defaultDb);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
    }
    
    public void Dispose()
    {
       
    }
    public IDatabase GetDatabase() => _db;
    
    // 示例：字符串存储
    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
        => await _db.StringSetAsync(key, value, expiry);
    
    // 示例：字符串读取
    public async Task<string> GetStringAsync(string key)
        => await _db.StringGetAsync(key);

    /// <summary>
    /// 根据ID数组批量获取JellyfinItem
    /// </summary>
    /// <param name="ids">Jellyfin项目ID列表</param>
    /// <returns>包含JellyfinItem的字典（key为原始ID）</returns>
    public async Task<Dictionary<string, JellyfinItem>> GetItemsByIds(IEnumerable<string> ids)
    {
        if (UseCache!="true")
        {
            return new Dictionary<string, JellyfinItem>();
        }
        if (ids == null || !ids.Any())
            return new Dictionary<string, JellyfinItem>();

        // 创建带前缀的Redis键
        var keys = ids.Select(id => (RedisKey)($"{KeyPrefix}{id}")).ToArray();
        
        // 批量获取值
        var values = await _db.StringGetAsync(keys);
        
        var result = new Dictionary<string, JellyfinItem>();
        
        for (int i = 0; i < keys.Length; i++)
        {
            if (!values[i].IsNull)
            {
                try
                {
                    // 反序列化JSON为JellyfinItem对象
                    var item = JsonSerializer.Deserialize<JellyfinItem>(values[i]!);
                    if (item != null)
                    {
                        // 从键名中提取原始ID（移除前缀）
                        var originalId = keys[i].ToString().Substring(KeyPrefix.Length);
                        result[originalId] = item;
                    }
                }
                catch (JsonException ex)
                {
                    // 处理反序列化错误
                    // 在实际应用中应该记录日志
                    Console.WriteLine($"反序列化错误: {ex.Message}");
                }
            }
        }
        
        return result;
    }
       /// <summary>
    /// 批量更新JellyfinItem缓存
    /// </summary>
    /// <param name="items">要更新的JellyfinItem列表</param>
    /// <param name="expiry">可选过期时间</param>
    public async Task UpdateItemsCache(IEnumerable<JellyfinItem> items, TimeSpan? expiry = null)
    {
        if (items == null || !items.Any())
            return;

        // 创建键值对列表
        var keyValuePairs = new List<KeyValuePair<RedisKey, RedisValue>>();
        
        foreach (var item in items)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id))
            {
                try
                {
                    // 序列化对象为JSON
                    var json = JsonSerializer.Serialize(item);
                    keyValuePairs.Add(new KeyValuePair<RedisKey, RedisValue>(
                        $"{KeyPrefix}{item.Id}", 
                        json
                    ));
                }
                catch (JsonException ex)
                {
                    // 处理序列化错误
                    Console.WriteLine($"序列化错误: {ex.Message}");
                }
            }
        }
        
        // 批量设置值
        var batch = _db.CreateBatch();
        
        foreach (var kvp in keyValuePairs)
        {
            batch.StringSetAsync(kvp.Key, kvp.Value, expiry);
        }
        
        batch.Execute();
    }
      /// <summary>
    /// 批量删除JellyfinItem缓存
    /// </summary>
    /// <param name="ids">要删除的Jellyfin项目ID列表</param>
    public async Task DeleteItemsCache(IEnumerable<string> ids)
    {
        if (ids == null || !ids.Any())
            return;

        // 创建带前缀的Redis键
        var keys = ids
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => (RedisKey)($"{KeyPrefix}{id}"))
            .ToArray();
        
        // 批量删除
        if (keys.Length > 0)
        {
            await _db.KeyDeleteAsync(keys);
        }
    }
}