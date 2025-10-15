using System.Text.Json;

namespace FacebookS;
using ListingDatabase = System.Collections.Generic.List<Listing>;

public class Database(string path)
{
    private ListingDatabase? _database;
    
    // Returns true if the listing was stored successfully, false if it already exists in the database
    public async Task<bool> StoreInDatabase(Listing listing)
    {
        _database ??= await LoadDatabase();
        
        if (_database.Contains(listing))
            return false;
    
        _database.Add(listing);
        await SaveDatabase();
        return true;
    }

    private async Task SaveDatabase()
    {
        if (_database == null) return;
        
        FilterOldListings();
        var json = JsonSerializer.Serialize(_database);
        await File.WriteAllTextAsync(path, json);
    }

    private void FilterOldListings()
    {
        if (_database == null) return;
        
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        _database.RemoveAll(listing => listing.Date < cutoff);
    }

    private async Task<ListingDatabase> LoadDatabase()
    {
        if (File.Exists(path))
            return JsonSerializer.Deserialize<ListingDatabase>(await File.ReadAllTextAsync(path))!;
    
        await File.WriteAllTextAsync(path, "[]");
        return [];
    }
}