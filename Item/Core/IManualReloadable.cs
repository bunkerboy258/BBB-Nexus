namespace BBBNexus
{
    public interface IManualReloadable
    {
        bool CanManualReload { get; }
        bool RequestManualReload();
    }
}
