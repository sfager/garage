// Generates the VAPID key pair story S-5's web push needs. Run once, then store the
// output with `dotnet user-secrets` — the private key must not reach source control.
var keys = WebPush.VapidHelper.GenerateVapidKeys();

Console.WriteLine("Garage:Vapid:PublicKey  = " + keys.PublicKey);
Console.WriteLine("Garage:Vapid:PrivateKey = " + keys.PrivateKey);
