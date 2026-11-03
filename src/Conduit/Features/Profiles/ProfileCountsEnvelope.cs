namespace Conduit.Features.Profiles;

/// <summary>
/// The counts shown on a profile. BookmarksCount is only ever set for the profile owner and stays null until bookmarks exist.
/// </summary>
public record ProfileCounts(int FavoritesCount, int? BookmarksCount);

public record ProfileCountsEnvelope(ProfileCounts Counts);
