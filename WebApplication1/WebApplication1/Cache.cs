namespace WebApplication1;

public class Cache : ICache
{
    private readonly Dictionary<string, GeoData> _cache = new();
    
    public void Set(string location, GeoData geoData)
    {
        _cache.Add(location, geoData);
    }

    public GeoData? Get(string location)
    {
        if (_cache.TryGetValue(location, out var data))
        {
            return data;
        }
        
        return null;
    }
}