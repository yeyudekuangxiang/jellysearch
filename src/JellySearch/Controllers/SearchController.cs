using System.Text.Json;
using JellySearch.Jellyfin;
using JellySearch.Models;
using JellySearch.Services;
using Meilisearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace JellySearch.Controllers;

[Route("[controller]")]
[ApiController]
public class SearchController : ControllerBase
{
    private string? CdnUrl { get; } = Environment.GetEnvironmentVariable("CDN_URl");
    private ILogger Log { get; set; }
    private JellyfinProxyService Proxy { get; }
    private Meilisearch.Index Index { get; }
    private JsonSerializerOptions DefaultJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public SearchController(ILoggerFactory logFactory, JellyfinProxyService proxy, Meilisearch.Index index)
    {
        this.Log = logFactory.CreateLogger<SearchController>();
        this.Proxy = proxy;
        this.Index = index;
    }

    /// <summary>
    /// Proxy all possible search URLs to the central Items endpoint
    /// </summary>
    /// <param name="searchTerm">The term that is searched for.</param>
    /// <param name="includeItemTypes">The type of item to search</param>
    /// <param name="userId">The user id of the current user</param>
    /// <returns></returns>
    [HttpGet("/Users/{userId}/Items")]
    [HttpGet("/Items")]
    [HttpGet("/Persons")]
    [HttpGet("/Artists/AlbumArtists")]
    [HttpGet("/Artists")]
    [HttpGet("/Genres")]
    [HttpGet("/Search/Hints")]
    public async Task<IActionResult> Search(
        [FromHeader(Name = "Authorization")] string? headerAuthorization,
        [FromHeader(Name = "X-Emby-Authorization")] string? legacyAuthorization,
        [FromHeader(Name = "X-Mediabrowser-Token")] string? legacyToken,
        [FromQuery]string? searchTerm,
        [FromRoute(Name = "UserId")] string? routeUserId,
        [FromQuery(Name = "UserId")] string? queryUserId)
    {
        Random random = new Random();
        var searchId =  random.Next();
        var searchStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        this.Log.LogInformation("{searchId}搜索开始:{time}",searchId,searchStartTime);
        // Get the requested path
        var path = this.Request.Path.Value ?? "";

        // Get the user id from either the route or the query
        var userId = routeUserId ?? queryUserId;

        // Get authorization from either the real "Authorization" header or from the legacy "X-Emby-Authorization" header
        var authorization = legacyAuthorization ?? headerAuthorization;

        if (Environment.GetEnvironmentVariable("JELLYSEARCH_DEBUG_REQUESTS") == "1")
        {
            Console.WriteLine("GET " + path + " from " + userId);
            Console.WriteLine("Using authorization: " + authorization);

            Console.WriteLine("HEADERS");
            foreach(var header in this.Request.Headers)
            {
                Console.WriteLine(header.Key + ": " + string.Join('|', header.Value));
            }

            Console.WriteLine("QUERY");
            foreach(var query in this.Request.Query)
            {
                Console.WriteLine(query.Key + ": " + string.Join('|', query.Value));
            }
        }

        if(authorization == null)
        {
            this.Log.LogWarning("Received request without Authorization header");
            return Content(JellyfinResponses.Empty, "application/json");
        }

        // If not searching, proxy directly for reverse proxies that cannot filter by query parameter
        // Genres are currently not supported
        if (searchTerm == null || path.EndsWith("/Genres", true, System.Globalization.CultureInfo.InvariantCulture))
        {
            // If the search term is empty, we will proxy directly
            this.Log.LogInformation("Proxying non-search request");

            var response = await this.Proxy.ProxyRequest(authorization, legacyToken, this.Request.Path, this.Request.QueryString.ToString());
            this.Log.LogInformation("{searchId}代理搜索耗时:{time}ms",  searchId, DateTimeOffset.Now.ToUnixTimeMilliseconds() - searchStartTime);
            if (response == null)
                return Content(JellyfinResponses.Empty, "application/json");
            else
                return Content(response, "application/json");
        }
        else
        {
            // Get all query arguments to pass along to Jellyfin
            // Remove searchterm since we already searched
            // Remove sortby and sortorder since we want to display results as Meilisearch returns them
            // Remove limit since we are requesting by specific IDs and don't want Jellyfin to remove some of them
            var query = this.Request.Query.Where(x =>
                !string.Equals(x.Key, "searchterm", StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(x.Key, "sortby", StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(x.Key, "sortorder", StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(x.Key, "limit", StringComparison.InvariantCultureIgnoreCase)
            ).ToDictionary(StringComparer.InvariantCultureIgnoreCase);

            // The default limit for search results as to not request too many IDs from Jellyfin
            // The default limit can't be exceeded but it can be reduced
            var limit = 50;
            var offset = 0;

            // Override the limit if it is less than the value defined above
            if (this.Request.Query.TryGetValue("Offset", out StringValues requestedOffset))
            {
                if (requestedOffset.Count == 1)
                {
                    if (int.TryParse(requestedOffset, out int parsed))
                    {
                        offset = parsed;
                    }
                    else
                    {
                        this.Log.LogWarning("Received invalid limit!");
                    }
                }
                else
                {
                    this.Log.LogWarning("Got more than one limit argument, ignoring");
                }
            }
            if (this.Request.Query.TryGetValue("Limit", out StringValues requestedLimit))
            {
                if (requestedLimit.Count == 1)
                {
                    if (int.TryParse(requestedLimit, out int parsed))
                    {
                        if (limit > parsed)
                        {
                            limit = parsed;
                        }
                    }
                    else
                    {
                        this.Log.LogWarning("Received invalid limit!");
                    }
                }
                else
                {
                    this.Log.LogWarning("Got more than one limit argument, ignoring");
                }
            }

            var includeItemTypes = new List<string>();

            if (query.TryGetValue("IncludeItemTypes", out StringValues includedTypes))
            {
                if (includedTypes.Count == 1)
                {
                    // If item count is 1, split by , and add all elements
                    includeItemTypes.AddRange(includedTypes[0].Split(','));
                }
                else
                {
                    // If item count is more than 1, add all elements directly
                    includeItemTypes.AddRange(includedTypes);
                }
            }

            var filteredTypes = new List<string>();
            var additionalFilters = new List<string>();

            if (includeItemTypes.Count == 0)
            {
                // Add types if no item types are provided
                if (path != null)
                {
                    // Handle direct endpoints and their types
                    if (path.EndsWith("/Persons", true, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Person");
                    }
                    else if (path.EndsWith("/Artists", true, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Audio.MusicArtist");
                    }
                    else if (path.EndsWith("/AlbumArtists", true, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Audio.MusicArtist");
                        additionalFilters.Add("isFolder = 1"); // Album artists are marked as folder
                    }
                    else if (path.EndsWith("/Genres", true, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Genre"); // TODO: Handle genre search properly
                    }
                }
            }
            else
            {
                // Get item type(s) from URL
                foreach (var includeItemType in includeItemTypes)
                {
                    var type = JellyfinHelper.GetFullItemType(includeItemType);

                    if (type == null)
                    {
                        this.Log.LogWarning("Got invalid type: {type}", includeItemType);
                    }
                    else
                    {
                        filteredTypes.Add(type);
                    }
                }
            }

            var items = new List<Item>();

            var searchQuery = new SearchQuery();
            var total = 0;
            if (searchTerm.StartsWith("lrc:", StringComparison.Ordinal))
            {
                searchTerm = searchTerm.Substring(4);
                searchQuery.AttributesToSearchOn = new[] { "lrcContent" };
            }
            else if (searchTerm.StartsWith("name:", StringComparison.Ordinal))
            {
                searchTerm = searchTerm.Substring(5);
                searchQuery.AttributesToSearchOn = new[] { "name" };
            }
            else if (searchTerm.StartsWith("artist:", StringComparison.Ordinal))
            {
                searchTerm = searchTerm.Substring(7);
                searchQuery.AttributesToSearchOn = new[] { "artists" };
            }
            searchQuery.Limit = limit;
            searchQuery.Offset = offset;
            if (filteredTypes.Count > 0)
            {
                // Loop through each requested type and search
                foreach (var filteredType in filteredTypes)
                {
                    var filter = "type = " + filteredType;

                    if (additionalFilters.Count > 0)
                    {
                        filter += " AND " + string.Join(" AND ", additionalFilters);
                    }

                    searchQuery.Filter = filter;

                    var results = await this.Index.SearchAsync<Item>(searchTerm, searchQuery);
                    var b = results as Meilisearch.SearchResult<Item>;
                    total = b.EstimatedTotalHits;
                    items.AddRange(results.Hits);
                }
            }
            else
            {
                // Search without filtering the type
                var results = await this.Index.SearchAsync<Item>(searchTerm, searchQuery);
                var b = results as Meilisearch.SearchResult<Item>;
                total = b.EstimatedTotalHits;
                items.AddRange(results.Hits);
            }

            this.Log.LogInformation("{searchId}美丽搜索耗时:{time}ms", searchId, DateTimeOffset.Now.ToUnixTimeMilliseconds() - searchStartTime);
            if (items.Count > 0)
            {
                this.Log.LogInformation("Proxying search request with {hits} results", items.Count);


                query.Add("ids", string.Join(',', items.Select(x => x.Guid.Replace("-", ""))));

                if (path.EndsWith("/Search/Hints", true, System.Globalization.CultureInfo.InvariantCulture))
                {
                    query.Add("fields", "PrimaryImageAspectRatio"); // Add more fields we need for search hints
                }
    
                var responseStream = await this.Proxy.ProxySearchRequest(authorization, legacyToken, userId, query);
                this.Log.LogInformation("{searchId}总搜索耗时:{time}ms",searchId,DateTimeOffset.Now.ToUnixTimeMilliseconds()-searchStartTime);
                if (path.EndsWith("/Search/Hints", true, System.Globalization.CultureInfo.InvariantCulture))
                {
                    // Handle search hints, expecting a root "SearchHints" array
                    // Restructure the Jellyfin result in a way that clients expecting search hints can work

                    if (responseStream == null)
                        return Content(JellyfinResponses.EmptySearchHints, "application/json");
                    else
                    {
                        // We need to deserialize in order to change the format for the search hint endpoint
                        var deserialized = await JsonSerializer.DeserializeAsync<JellyfinItemResponse<JellyfinItem>>(responseStream);

                        if (deserialized.TotalRecordCount == 0)
                            return Content(JellyfinResponses.EmptySearchHints, "application/json");
                        
                        foreach (var item in deserialized.Items)
                        {
                            // Modify items for search hints

                            if (item.ImageTags != null & item.ImageTags.ContainsKey("Primary"))
                            {
                                item.PrimaryImageTag = item.ImageTags["Primary"];
                                item.ImageTags = null;
                            }

                            // Try to get the parent back drop first and overwrite it if a item backdrop is available as well
                            // BackdropImageTag sometimes does not get returned on the Items endpoint
                            if (item.ParentBackdropImageTags != null && item.ParentBackdropImageTags.Count > 0)
                            {
                                item.BackdropImageTag = item.ParentBackdropImageTags[0];
                                item.ParentBackdropImageTags = null;
                            }
                            if (item.BackdropImageTags != null && item.BackdropImageTags.Count > 0)
                            {
                                item.BackdropImageTag = item.BackdropImageTags[0];
                                item.BackdropImageTags = null;
                            }

                            if (item.AlbumId != null)
                            {
                                item.BackdropImageItemId = item.AlbumId;
                                //item.AlbumId = null;
                            }
                            else if (item.ParentBackdropItemId != null)
                            {
                                item.BackdropImageItemId = item.ParentBackdropItemId;
                                item.ParentBackdropItemId = null;
                            }
                            else
                            {
                                item.BackdropImageItemId = item.Id;
                            }

                            if (item.SeriesName != null)
                            {
                                item.Series = item.SeriesName;
                                item.SeriesName = null;
                            }

                            item.ItemId = item.Id; // ItemId is deprecated but still set
                            item.IsFolder = null;
                            item.BackdropImageTags = null;
                        }

                        var searchHintResponse = new JellyfinSearchHintResponse()
                        {
                            SearchHints = deserialized.Items,
                            TotalRecordCount = total,
                        };

                        using Stream outputStream = new MemoryStream();
                        await JsonSerializer.SerializeAsync(outputStream, searchHintResponse, this.DefaultJsonOptions);
                        outputStream.Position = 0;

                        return Content(await new StreamReader(outputStream).ReadToEndAsync(), "application/json");
                    }
                }
                else
                {

                    // Handle most Jellyfin routes expecting a root "Items" array
                    // We need to deserialize in order to change the format for the search hint endpoint
                    
                    if (responseStream == null)
                        return Content(JellyfinResponses.Empty, "application/json");
                    else
                    {
                        var deserialized = await JsonSerializer.DeserializeAsync<JellyfinItemResponse<Object>>(responseStream);
                        deserialized.TotalRecordCount = total;
                        string respBody = JsonSerializer.Serialize(deserialized, this.DefaultJsonOptions);
                        return Content(respBody, "application/json");
                    }
                     
                }
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return Content(JellyfinResponses.Empty, "application/json");
            }
            
        }
    }
    [HttpGet("/Audio/{audioId}/stream")]
    public async Task<IActionResult> CdnController()
    {
         // 获取当前请求的完整URL
        var currentUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        if (string.IsNullOrEmpty(CdnUrl))
        {

        }
       
    
    // 对URL进行编码
    var encodedUrl = System.Net.WebUtility.UrlEncode(currentUrl);
    
    // 目标域名（请替换为实际域名）
    string targetDomain = CdnUrl;
    
    // 构建重定向URL，将编码后的URL作为参数
    var redirectUrl = $"{targetDomain}?url={encodedUrl}";
    
    // 302重定向
    return Redirect(redirectUrl);
    }
}