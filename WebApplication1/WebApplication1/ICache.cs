namespace WebApplication1;

public interface ICache
{
    public void Set(string location, GeoData geoData);
    public GeoData? Get(string location);
}