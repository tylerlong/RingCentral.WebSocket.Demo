using RingCentral;
using dotenv.net;
using RingCentral.Net.WebSocket;

var envVars = DotEnv.Read();

var rc = new RestClient(envVars["RINGCENTRAL_CLIENT_ID"], envVars["RINGCENTRAL_CLIENT_SECRET"], envVars["RINGCENTRAL_SERVER_URL"]);
await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
Console.WriteLine(rc.token.access_token);

var wsExtension = new WebSocketExtension();
await rc.InstallExtension(wsExtension);
await wsExtension.Subscribe(new string[] {"/restapi/v1.0/account/~/extension/~/message-store"}, message =>
{
    Console.WriteLine(message);
});

var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
while (await timer.WaitForNextTickAsync())
{
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
}

await rc.Revoke();
