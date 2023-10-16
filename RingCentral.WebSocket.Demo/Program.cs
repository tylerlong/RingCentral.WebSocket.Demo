using RingCentral;
using dotenv.net;
using RingCentral.Net.WebSocket;

var envVars = DotEnv.Read();

var rc = new RestClient(envVars["RINGCENTRAL_CLIENT_ID"], envVars["RINGCENTRAL_CLIENT_SECRET"], envVars["RINGCENTRAL_SERVER_URL"]);
await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
Console.WriteLine(rc.token.access_token);

var wsExtension = new WebSocketExtension(new WebSocketOptions
{
    debugMode = false
});
await rc.InstallExtension(wsExtension);
await wsExtension.Subscribe(new string[] {"/restapi/v1.0/account/~/extension/~/message-store"}, message =>
{
    Console.WriteLine(message);
});

// Trigger some notifications for testing purpose
var timer = new PeriodicTimer(TimeSpan.FromMinutes(40));
while (await timer.WaitForNextTickAsync())
{
    await rc.Refresh();
    // Check if the connection has closed
    if (!wsExtension.ws.IsRunning)
    {
        await wsExtension.Reconnect();
    }
    await rc.Restapi().Account().Extension().CompanyPager().Post(new CreateInternalTextMessageRequest
    {
        text = "Hello world",
        from = new PagerCallerInfoRequest
        {
            extensionId = rc.token.owner_id
        },
        to = new []{ new PagerCallerInfoRequest
        {
            extensionId = rc.token.owner_id
        }}
    });
    Console.WriteLine("Pager sent at " + DateTime.Now.ToString("yyyyMMdd HHmmss"));
}

await rc.Revoke();
