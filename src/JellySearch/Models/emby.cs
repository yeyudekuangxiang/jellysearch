using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace JellySearch.Models;

public class EmbyAudioSearchResponse
{
    [JsonPropertyName("Items")]
    public List<Object> Items { get; set; }

    [JsonPropertyName("TotalRecordCount")]
    public int TotalRecordCount { get; set; }
}

public class EmbyAudioItem
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("ServerId")]
    public string ServerId { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("DateCreated")]
    public DateTime DateCreated { get; set; }

    [JsonPropertyName("Container")]
    public string Container { get; set; }

    [JsonPropertyName("SortName")]
    public string SortName { get; set; }

    [JsonPropertyName("MediaSources")]
    public List<MediaSource> MediaSources { get; set; }

    [JsonPropertyName("RunTimeTicks")]
    public long RunTimeTicks { get; set; }

    [JsonPropertyName("Size")]
    public long Size { get; set; }

    [JsonPropertyName("Bitrate")]
    public int Bitrate { get; set; }

    [JsonPropertyName("ProductionYear")]
    public int? ProductionYear { get; set; }

    [JsonPropertyName("IndexNumber")]
    public int? IndexNumber { get; set; }

    [JsonPropertyName("ParentIndexNumber")]
    public int? ParentIndexNumber { get; set; }

    [JsonPropertyName("IsFolder")]
    public bool IsFolder { get; set; }

    [JsonPropertyName("Type")]
    public string Type { get; set; }

    [JsonPropertyName("UserData")]
    public UserData UserData { get; set; }

    [JsonPropertyName("Artists")]
    public List<string> Artists { get; set; }

    [JsonPropertyName("ArtistItems")]
    public List<ArtistItem> ArtistItems { get; set; }

    [JsonPropertyName("Composers")]
    public List<string> Composers { get; set; }

    [JsonPropertyName("Album")]
    public string Album { get; set; }

    [JsonPropertyName("AlbumId")]
    public string AlbumId { get; set; }

    [JsonPropertyName("AlbumArtist")]
    public string AlbumArtist { get; set; }

    [JsonPropertyName("AlbumArtists")]
    public List<AlbumArtist> AlbumArtists { get; set; }

    [JsonPropertyName("ImageTags")]
    public ImageTags ImageTags { get; set; }

    [JsonPropertyName("MediaType")]
    public string MediaType { get; set; }
}

public class MediaSource
{
    [JsonPropertyName("Chapters")]
    public List<object> Chapters { get; set; }

    [JsonPropertyName("Protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("Path")]
    public string Path { get; set; }

    [JsonPropertyName("Type")]
    public string Type { get; set; }

    [JsonPropertyName("Container")]
    public string Container { get; set; }

    [JsonPropertyName("Size")]
    public long Size { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("IsRemote")]
    public bool IsRemote { get; set; }

    [JsonPropertyName("HasMixedProtocols")]
    public bool HasMixedProtocols { get; set; }

    [JsonPropertyName("RunTimeTicks")]
    public long RunTimeTicks { get; set; }

    [JsonPropertyName("SupportsTranscoding")]
    public bool SupportsTranscoding { get; set; }

    [JsonPropertyName("SupportsDirectStream")]
    public bool SupportsDirectStream { get; set; }

    [JsonPropertyName("SupportsDirectPlay")]
    public bool SupportsDirectPlay { get; set; }

    [JsonPropertyName("IsInfiniteStream")]
    public bool IsInfiniteStream { get; set; }

    [JsonPropertyName("RequiresOpening")]
    public bool RequiresOpening { get; set; }

    [JsonPropertyName("RequiresClosing")]
    public bool RequiresClosing { get; set; }

    [JsonPropertyName("RequiresLooping")]
    public bool RequiresLooping { get; set; }

    [JsonPropertyName("SupportsProbing")]
    public bool SupportsProbing { get; set; }

    [JsonPropertyName("MediaStreams")]
    public List<MediaStream> MediaStreams { get; set; }

    [JsonPropertyName("Formats")]
    public List<object> Formats { get; set; }

    [JsonPropertyName("Bitrate")]
    public int Bitrate { get; set; }

    [JsonPropertyName("RequiredHttpHeaders")]
    public Dictionary<string, string> RequiredHttpHeaders { get; set; }

    [JsonPropertyName("AddApiKeyToDirectStreamUrl")]
    public bool AddApiKeyToDirectStreamUrl { get; set; }

    [JsonPropertyName("ReadAtNativeFramerate")]
    public bool ReadAtNativeFramerate { get; set; }

    [JsonPropertyName("DefaultAudioStreamIndex")]
    public int? DefaultAudioStreamIndex { get; set; }

    [JsonPropertyName("DefaultSubtitleStreamIndex")]
    public int? DefaultSubtitleStreamIndex { get; set; }

    [JsonPropertyName("ItemId")]
    public string ItemId { get; set; }
}

public class MediaStream
{
    [JsonPropertyName("Codec")]
    public string Codec { get; set; }

    [JsonPropertyName("TimeBase")]
    public string TimeBase { get; set; }

    [JsonPropertyName("DisplayTitle")]
    public string DisplayTitle { get; set; }

    [JsonPropertyName("IsInterlaced")]
    public bool IsInterlaced { get; set; }

    [JsonPropertyName("ChannelLayout")]
    public string ChannelLayout { get; set; }

    [JsonPropertyName("BitRate")]
    public int? BitRate { get; set; }

    [JsonPropertyName("BitDepth")]
    public int? BitDepth { get; set; }

    [JsonPropertyName("Channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("SampleRate")]
    public int? SampleRate { get; set; }

    [JsonPropertyName("IsDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("IsForced")]
    public bool IsForced { get; set; }

    [JsonPropertyName("IsHearingImpaired")]
    public bool IsHearingImpaired { get; set; }

    [JsonPropertyName("Type")]
    public string Type { get; set; }

    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("IsExternal")]
    public bool IsExternal { get; set; }

    [JsonPropertyName("IsTextSubtitleStream")]
    public bool IsTextSubtitleStream { get; set; }

    [JsonPropertyName("SupportsExternalStream")]
    public bool SupportsExternalStream { get; set; }

    [JsonPropertyName("Protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("ExtendedVideoType")]
    public string ExtendedVideoType { get; set; }

    [JsonPropertyName("ExtendedVideoSubType")]
    public string ExtendedVideoSubType { get; set; }

    [JsonPropertyName("ExtendedVideoSubTypeDescription")]
    public string ExtendedVideoSubTypeDescription { get; set; }

    [JsonPropertyName("AttachmentSize")]
    public int AttachmentSize { get; set; }

    [JsonPropertyName("ColorSpace")]
    public string ColorSpace { get; set; }

    [JsonPropertyName("Comment")]
    public string Comment { get; set; }

    [JsonPropertyName("RefFrames")]
    public int? RefFrames { get; set; }

    [JsonPropertyName("Height")]
    public int? Height { get; set; }

    [JsonPropertyName("Width")]
    public int? Width { get; set; }

    [JsonPropertyName("RealFrameRate")]
    public double? RealFrameRate { get; set; }

    [JsonPropertyName("Profile")]
    public string Profile { get; set; }

    [JsonPropertyName("AspectRatio")]
    public string AspectRatio { get; set; }

    [JsonPropertyName("PixelFormat")]
    public string PixelFormat { get; set; }

    [JsonPropertyName("Level")]
    public int? Level { get; set; }

    [JsonPropertyName("IsAnamorphic")]
    public bool? IsAnamorphic { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; }

    [JsonPropertyName("Path")]
    public string Path { get; set; }
}

public class UserData
{
    [JsonPropertyName("PlaybackPositionTicks")]
    public long PlaybackPositionTicks { get; set; }

    [JsonPropertyName("PlayCount")]
    public int PlayCount { get; set; }

    [JsonPropertyName("IsFavorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("Played")]
    public bool Played { get; set; }
}

public class ArtistItem
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }
}

public class AlbumArtist
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }
}

public class ImageTags
{
    [JsonPropertyName("Primary")]
    public string Primary { get; set; }
}



