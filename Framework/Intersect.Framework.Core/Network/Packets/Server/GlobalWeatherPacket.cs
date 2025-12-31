using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class GlobalWeatherPacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public GlobalWeatherPacket()
    {
    }

    public GlobalWeatherPacket(Guid animationId, int xSpeed, int ySpeed, int intensity)
    {
        AnimationId = animationId;
        XSpeed = xSpeed;
        YSpeed = ySpeed;
        Intensity = intensity;
    }

    [Key(0)]
    public Guid AnimationId { get; set; }

    [Key(1)]
    public int XSpeed { get; set; }

    [Key(2)]
    public int YSpeed { get; set; }

    [Key(3)]
    public int Intensity { get; set; }
}
