﻿using System.ComponentModel.DataAnnotations.Schema;

using Intersect.GameObjects.Crafting;
using Intersect.Models;

using Newtonsoft.Json;

namespace Intersect.GameObjects;

public partial class CraftingTableBase : DatabaseObject<CraftingTableBase>, IFolderable
{
    [NotMapped]
    public DbList<CraftBase> Crafts { get; set; } = [];

    [JsonConstructor]
    public CraftingTableBase(Guid id) : base(id)
    {
        Name = "New Table";
    }

    //Parameterless constructor for EF
    public CraftingTableBase()
    {
        Name = "New Table";
    }

    [JsonIgnore]
    [Column("Crafts")]
    public string CraftsJson
    {
        get => JsonConvert.SerializeObject(Crafts, Formatting.None);
        protected set => Crafts = JsonConvert.DeserializeObject<DbList<CraftBase>>(value);
    }

    /// <inheritdoc />
    public string Folder { get; set; } = string.Empty;
}
