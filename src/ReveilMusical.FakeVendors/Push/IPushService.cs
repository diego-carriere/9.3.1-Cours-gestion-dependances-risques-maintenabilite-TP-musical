namespace ReveilMusical.FakeVendors.Push;

public interface IPushService
{
    /// <summary>
    /// Le style de ce SDK : « fire and forget », le résultat arrive plus tard par
    /// <paramref name="onCompleted"/>, sur un autre thread.
    /// </summary>
    public void Deliver(PushRequest request, Action<PushDeliveryReport> onCompleted);
}
