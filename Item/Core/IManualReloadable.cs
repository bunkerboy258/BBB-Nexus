namespace BBBNexus
{
    public interface IManualReloadable
    {
        bool CanManualReload { get; }
        bool RequestManualReload();
    }

    public interface IAiReloadable
    {
        int CurrentMagazine { get; }
        int MagazineCapacity { get; }
        bool IsReloading { get; }
        bool RequestManualReload(int targetMagazineCount);
    }
}
