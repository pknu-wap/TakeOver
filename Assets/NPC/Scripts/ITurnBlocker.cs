namespace TakeOver.NPC
{
    public interface ITurnBlocker
    {
        bool IsBlocking(out string reason);
    }
}
