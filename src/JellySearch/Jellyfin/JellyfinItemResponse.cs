namespace JellySearch.Jellyfin;

public class JellyfinItemResponse<T>
{
    public List<T> Items { get; set; }

    public int TotalRecordCount { get; set; }
    public int StartIndex { get; set; }
}
