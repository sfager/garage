namespace Garage.Web.Api.Contracts;

public record NotificationStatusResponse(bool IsConfigured, string PublicKey, int DeviceCount);

public record UnsubscribeRequest(string Endpoint);
