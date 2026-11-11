namespace PhotoSite.Web.Components.Services;

public sealed class AlbumCardRevealState
{
    public event Action? Changed;

    public int? RevealedAlbumId { get; private set; }

    public void Reveal(int albumId)
    {
        if (RevealedAlbumId == albumId) return;
        RevealedAlbumId = albumId;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (RevealedAlbumId == null) return;
        RevealedAlbumId = null;
        Changed?.Invoke();
    }
}

