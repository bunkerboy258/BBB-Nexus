namespace BBBNexus
{
    public interface INavigatorSensor
    {
        ref readonly NavigationContext GetCurrentContext();
    }
}