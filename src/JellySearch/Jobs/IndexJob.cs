using Quartz;
using Microsoft.Data.Sqlite;
using JellySearch.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;

namespace JellySearch.Jobs;
public class MediaItem
{
    public bool HasLyrics { get; set; }
    public List<string> LyricFiles { get; set; }
}

public class IndexJob : IJob
{
    private string? JellyfinConfigDir { get; } = Environment.GetEnvironmentVariable("JELLYFIN_CONFIG_DIR");
    private string? AssestPathReplace { get; } = Environment.GetEnvironmentVariable("ASSEST_PATH_REPLACE"); // [{"/volume1":"Z:","/":"\\"}]
    private string? IndexLrc { get; } = Environment.GetEnvironmentVariable("INDEX_LRC");
    private ILogger? Log { get; set; }
    private List<Dictionary<string, string>>? pathReplaceObj { get; set; }
    public async Task Execute(IJobExecutionContext context)
    {
        var jobData = context.JobDetail.JobDataMap;
        var index = jobData["index"] as Meilisearch.Index;

        var logFactory = jobData["logFactory"] as ILoggerFactory;
        this.Log = logFactory.CreateLogger<IndexJob>();
      
        try
        {
            this.Log.LogInformation("Indexing items...");

            // Set filterable attributes
            await index.UpdateFilterableAttributesAsync(
                new string[] { "type", "parentId", "isFolder" }
            );

            // Set sortable attributes
            await index.UpdateSortableAttributesAsync(
                new string[] { "communityRating", "criticRating" }
            );

            // Change priority of fields; Meilisearch always uses camel case!
            await index.UpdateSearchableAttributesAsync(
                new string[] { "name", "artists", "albumArtists", "originalTitle", "lrcContent", "productionYear", "seriesName", "genres", "tags", "studios", "overview" }
            );

            // We only need the GUID to pass to Jellyfin
            await index.UpdateDisplayedAttributesAsync(
                new string[] { "guid", "name" }
            );

            // Set ranking rules to add critic rating
            await index.UpdateRankingRulesAsync(
                new string[] { "exactness", "proximity", "attribute", "words", "typo", "sort", "communityRating:desc", "criticRating:desc" }
            );

            var legacy = true;
            var databasePath = "/data/library.db";

            this.Log.LogInformation(this.JellyfinConfigDir + databasePath);

            // If the old database does not exist, use the new one
            if (!File.Exists(Path.Join(this.JellyfinConfigDir + databasePath)))
            {
                this.Log.LogInformation("No library.db available, trying jellyfin.db");

                legacy = false;
                databasePath = "/data/jellyfin.db";
            }

            // If the new database doesn't exist either, abort
            if (!File.Exists(Path.Join(this.JellyfinConfigDir + databasePath)))
            {
                throw new FileNotFoundException("Could not find either library.db or jellyfin.db in config folder.");
            }

            this.Log.LogInformation("连接数据库");
            // Open Jellyfin library
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = this.JellyfinConfigDir + databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            };

            using var connection = new SqliteConnection(connectionString.ToString());
            await connection.OpenAsync();

            // Query all base items
            using var command = connection.CreateCommand();

            // Adjust query if querying a legacy database
            if (legacy)
                command.CommandText = "SELECT guid, type, ParentId, CommunityRating, Name, Overview, ProductionYear, Genres, Studios, Tags, IsFolder, CriticRating, OriginalTitle, SeriesName, Artists, AlbumArtists, data FROM TypedBaseItems";
            else
                command.CommandText = "SELECT id, Type, ParentId, CommunityRating, Name, Overview, ProductionYear, Genres, Studios, Tags, IsFolder, CriticRating, OriginalTitle, SeriesName, Artists, AlbumArtists, data FROM BaseItems";

            this.Log.LogInformation("查询数据");
            using var reader = await command.ExecuteReaderAsync();

            var items = new List<Item>();
            this.Log.LogInformation("遍历数据");
            while (await reader.ReadAsync())
            {
                try
                {
                    var item = new Item()
                    {
                        Guid = reader.GetGuid(0).ToString(),
                        Type = !reader.IsDBNull(1) ? reader.GetString(1) : null,
                        ParentId = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                        CommunityRating = !reader.IsDBNull(3) ? reader.GetInt16(3) : null,
                        Name = !reader.IsDBNull(4) ? reader.GetString(4) : null,
                        Overview = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                        ProductionYear = !reader.IsDBNull(6) ? reader.GetInt32(6) : null,
                        Genres = !reader.IsDBNull(7) ? reader.GetString(7).Split('|') : null,
                        Studios = !reader.IsDBNull(8) ? reader.GetString(8).Split('|') : null,
                        Tags = !reader.IsDBNull(9) ? reader.GetString(9).Split('|') : null,
                        IsFolder = !reader.IsDBNull(10) ? reader.GetInt16(10) : null,
                        CriticRating = !reader.IsDBNull(11) ? reader.GetInt16(11) : null,
                        OriginalTitle = !reader.IsDBNull(12) ? reader.GetString(12) : null,
                        SeriesName = !reader.IsDBNull(13) ? reader.GetString(13) : null,
                        Artists = !reader.IsDBNull(14) ? reader.GetString(14).Split('|') : null,
                        AlbumArtists = !reader.IsDBNull(15) ? reader.GetString(15).Split('|') : null,
                        LrcContent = this.GetFirstLyricContent(!reader.IsDBNull(16) ? reader.GetString(16) : null)
                    };
                    items.Add(item);
                }
                catch (Exception e)
                {
                    this.Log.LogError("Could not add an item to the index, ignoring item");
                    this.Log.LogError("Item index: " + (items.Count - 1));
                    if (!reader.IsDBNull(4))
                        this.Log.LogError("Item name: " + reader.GetString(4));

                    this.Log.LogDebug(e.Message);
                    this.Log.LogDebug(e.StackTrace);
                }
            }


            this.Log.LogInformation("更新文档");
            if (items.Count > 0)
            {
                // Add items to search index in batches
                await index.AddDocumentsInBatchesAsync<Item>(items, 5000, "guid");
            }

            this.Log.LogInformation("Indexed {count} items, it might take a few moments for Meilisearch to finish indexing", items.Count);
        }
        catch (Exception e)
        {
            throw e;
        }
    }
    public string replace(string path)
    {
        if (string.IsNullOrEmpty(path) || (string.IsNullOrEmpty(AssestPathReplace)))
            return path;
        
        try
        {
            if (pathReplaceObj==null){
                this.Log.LogInformation(AssestPathReplace);
                pathReplaceObj =  JsonSerializer.Deserialize<List<Dictionary<string, string>>>(AssestPathReplace) ?? new List<Dictionary<string, string>>();
            }
            

            if (!pathReplaceObj.Any())
            {
                this.Log?.LogInformation("无替换规则");
                return path;
            }

        // 5. 使用StringBuilder提高性能
            var result = new StringBuilder(path);
        
        // 6. 按规则顺序替换
            foreach (var dict in pathReplaceObj)
            {
                foreach (var kvp in dict)
                {
                // 跳过无效键
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                
                // 处理空值（替换为空字符串）
                    string value = kvp.Value ?? string.Empty;
                
                // 执行替换（考虑大小写不敏感）
                    result.Replace(kvp.Key, value);
                }
            }

            return result.ToString();
        }
        catch (JsonException ex)
        {
            this.Log.LogError($"JSON 解析错误: {ex.Message}");
        }
        return path;
    }
        /// <summary>
    /// 从JSON字符串中解析并读取第一个歌词文件的内容
    /// </summary>
    /// <param name="json">包含媒体信息的JSON字符串</param>
    /// <returns>第一个歌词文件的内容，如果出错或不存在则返回null</returns>
    public string GetFirstLyricContent(string json)
    {
        if (IndexLrc!="true"){
            return null;
        }
       
        try
        {
            if (json==null){
                return null;
            }
            // 解析JSON
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            MediaItem item = JsonSerializer.Deserialize<MediaItem>(json, options);

            // 检查是否有歌词文件
            if (item?.HasLyrics != true || item.LyricFiles == null || item.LyricFiles.Count == 0)
            {
                return null;
            }

            // 获取第一个歌词文件路径
            string lyricPath = item.LyricFiles[0];
            
            // 路径处理：Unicode转义序列会自动转换，不需要额外操作
            // 实际使用时可能需要根据运行环境调整路径格式
            string fullPath = this.replace(lyricPath);
            //this.Log.LogInformation(fullPath);
            // 读取文件内容
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
            return null;
        }
        catch (JsonException e)
        {
            this.Log.LogError(json);
            this.Log.LogError("{message}", e.Message);
            this.Log.LogError("{stacktrace}", e.StackTrace);
            // JSON解析错误
            return null;
        }
        catch (Exception e)
        {
            this.Log.LogError(json);
            this.Log.LogError("{message}", e.Message);
            this.Log.LogError("{stacktrace}", e.StackTrace);
            // 文件读取错误、权限问题等
            return null;
        }
    }
}
