namespace FacebookS;

public record Listing(string Title, string Link, string ImageLink)
{
    // Change the equality comparison to only compare the link property
    public virtual bool Equals(Listing? other) => other is not null && Link == other.Link;
    public override int GetHashCode() => Link.GetHashCode();
}