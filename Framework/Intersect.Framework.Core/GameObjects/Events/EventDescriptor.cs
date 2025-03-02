using System.ComponentModel.DataAnnotations.Schema;
using Intersect.Framework.Core.Serialization;
using Intersect.Models;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.GameObjects.Events;

public partial class EventDescriptor : DatabaseObject<EventDescriptor>, IFolderable
{
    //Cached Pages Data
    private string mCachedPagesData = null;

    //Event Pages
    private List<EventPage> mPages = [];

    //EF Parameterless Constructor
    public EventDescriptor()
    {
    }

    [JsonConstructor]
    public EventDescriptor(Guid id, Guid mapId, int x, int y, bool isCommon = false, bool isGlobal = false) : base(id)
    {
        Name = "New Event";
        MapId = mapId;
        if (isCommon)
        {
            Name = "New Common Event";
        }

        SpawnX = x;
        SpawnY = y;
        CommonEvent = isCommon;
        Global = isGlobal;
        Pages = [new()];
    }

    public EventDescriptor(Guid id, bool isCommon = false) : base(id)
    {
        Name = "New Event";
        Pages = [];
        CommonEvent = isCommon;
    }

    public EventDescriptor(Guid id, EventDescriptor copy) : base(id)
    {
        Name = "New Event";
        Pages = [];
        Load(copy.JsonData);
        CommonEvent = copy.CommonEvent;
    }

    public EventDescriptor(Guid id, string json, bool isCommon = false) : base(id)
    {
        Name = "New Event";
        CommonEvent = isCommon;
        Pages = [];
        Load(json);
    }

    public Guid MapId { get; set; }

    public int SpawnX { get; set; } = -1;

    public int SpawnY { get; set; } = -1;

    public bool CommonEvent { get; set; }

    /// <summary>
    /// Option used to run events even when another is already running
    /// </summary>
    public bool CanRunInParallel { get; set; }

    public bool Global { get; set; }

    [JsonIgnore]
    [Column("Pages")]
    public string PagesJson
    {
        get => mCachedPagesData;
        protected set => Pages = JsonConvert.DeserializeObject<List<EventPage>>(
            value,
            new JsonSerializerSettings()
            {
                SerializationBinder = new IntersectTypeSerializationBinder(),
                TypeNameHandling = TypeNameHandling.Auto,
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            }
        );
    }

    [NotMapped]
    public List<EventPage> Pages
    {
        get => mPages;
        set
        {
            mPages = value;
            mCachedPagesData = JsonConvert.SerializeObject(
                Pages,
                new JsonSerializerSettings()
                {
                    SerializationBinder = new IntersectTypeSerializationBinder(),
                    TypeNameHandling = TypeNameHandling.Auto,
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                }
            );
        }
    }

    public new static string[] Names => Lookup.Where(pair => ((EventDescriptor) pair.Value)?.CommonEvent ?? false)
        .OrderBy(p => p.Value?.Name)
        .Select(pair => pair.Value?.Name ?? Deleted)
        .ToArray();

    public new static KeyValuePair<Guid, string>[] ItemPairs => Lookup
        .Where(pair => ((EventDescriptor) pair.Value)?.CommonEvent ?? false)
        .OrderBy(p => p.Value?.Name)
        .Select(pair => new KeyValuePair<Guid, string>(pair.Key, pair.Value?.Name ?? Deleted))
        .ToArray();

    [JsonIgnore]
    [NotMapped]
    public override string JsonData => JsonConvert.SerializeObject(
        this, Formatting.Indented,
        new JsonSerializerSettings()
        {
            SerializationBinder = new IntersectTypeSerializationBinder(),
            TypeNameHandling = TypeNameHandling.Auto, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        }
    );

    /// <inheritdoc />
    public string Folder { get; set; } = string.Empty;

    public static new Guid IdFromList(int listIndex)
    {
        if (listIndex < 0)
        {
            return Guid.Empty;
        }

        var commonEvents = Lookup.Where(pair => ((EventDescriptor) pair.Value)?.CommonEvent ?? false)
            .OrderBy(p => p.Value?.Name)
            .ToArray();

        if (listIndex > commonEvents.Length)
        {
            return Guid.Empty;
        }

        return commonEvents[listIndex].Value?.Id ?? Guid.Empty;
    }

    public static new EventDescriptor FromList(int listIndex)
    {
        return Get(IdFromList(listIndex));
    }

    public static new int ListIndex(Guid id)
    {
        var commonEvents = Lookup.Where(pair => ((EventDescriptor) pair.Value)?.CommonEvent ?? false)
            .OrderBy(p => p.Value?.Name)
            .ToArray();

        for (var i = 0; i < commonEvents.Length; i++)
        {
            if (commonEvents[i].Key == id)
            {
                return i;
            }
        }

        return -1;
    }

    public new int ListIndex()
    {
        return ListIndex(Id);
    }

    public override void Load(string json, bool keepCreationTime = false)
    {
        var oldTime = TimeCreated;
        JsonConvert.PopulateObject(
            json,
            this,
            new JsonSerializerSettings()
            {
                SerializationBinder = new IntersectTypeSerializationBinder(),
                TypeNameHandling = TypeNameHandling.Auto,
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            }
        );

        if (keepCreationTime)
        {
            TimeCreated = oldTime;
        }
    }
}
