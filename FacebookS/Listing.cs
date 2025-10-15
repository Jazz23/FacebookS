namespace FacebookS;

public record Listing(string Title, string Link, string ImageLink, long Date)
{
    public Listing(string Title, string Link, string ImageLink)
        : this(Title, Link, ImageLink, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    {
    }
    
    // Default constructor
    public Listing() : this(string.Empty, string.Empty, string.Empty, 0) { }
    
    // Change the equality comparison to only compare the link property
    public virtual bool Equals(Listing? other) => other is not null && Link == other.Link;
    public override int GetHashCode() => Link.GetHashCode();
}