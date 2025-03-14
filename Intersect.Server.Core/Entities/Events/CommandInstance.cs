using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Server.Entities.Events;


public partial class CommandInstance
{

    public enum EventResponse
    {

        None = 0,

        Dialogue,

        Shop,

        Bank,

        Crafting,

        Quest,

        Timer,

        Picture,

        Fade

    }

    public Guid[]
        BranchIds = null; //Potential Branches for Commands that require responses such as ShowingOptions or Offering a Quest

    public EventCommand Command;

    private int commandIndex;

    public List<EventCommand> CommandList;

    public Guid CommandListId;

    public EventPage Page;

    public EventResponse WaitingForResponse = EventResponse.None;

    public Guid WaitingForRoute;

    public Guid WaitingForRouteMap;

    public EventCommand WaitingOnCommand = null;

    public CommandInstance(EventPage page, int listIndex = 0)
    {
        Page = page;
        CommandList = page.CommandLists.Values.First();
        CommandIndex = listIndex;
    }

    public CommandInstance(EventPage page, List<EventCommand> commandList, int listIndex = 0)
    {
        Page = page;
        CommandList = commandList;
        CommandIndex = listIndex;
    }

    public CommandInstance(EventPage page, Guid commandListId, int listIndex = 0)
    {
        Page = page;
        CommandList = page.CommandLists[commandListId];
        CommandIndex = listIndex;
    }

    public int CommandIndex
    {
        get => commandIndex;
        set
        {
            commandIndex = value;
            Command = commandIndex >= 0 && commandIndex < CommandList.Count ? CommandList[commandIndex] : null;
        }
    }

}
