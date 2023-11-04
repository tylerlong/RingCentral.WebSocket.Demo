using System.Timers;
using RingCentral;
using dotenv.net;
using RingCentral.Net.WebSocket;

var envVars = DotEnv.Read();

var rc = new RestClient(envVars["RINGCENTRAL_CLIENT_ID"], envVars["RINGCENTRAL_CLIENT_SECRET"], envVars["RINGCENTRAL_SERVER_URL"]);
await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);

// install the WebSocket extension
var wsExtension = new WebSocketExtension(new WebSocketOptions
{
    debugMode = true // enable this will print all the WebSocket messages
});
await rc.InstallExtension(wsExtension);

// To refresh token every 30 minutes
// This is necessary, if the token expires, eventually your WebSocket connection will stop working
// By default token expires every hour, it is perfectly fine to refresh it every 30 minutes. There is no need to to refresh it at a shorter interval
var aTimer = new System.Timers.Timer();
aTimer.Elapsed += new ElapsedEventHandler(async (object source, ElapsedEventArgs e) =>
{
    try
    {
        await rc.Refresh();
    }
    // in theory this should not happen, but just in case
    catch
    {
        await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
        // You will need a new WS session, since you create a new OAuth session
        await wsExtension.Reconnect();
    }
});
aTimer.Interval = 1800000; // 30 minutes
aTimer.Enabled = true;

// subscribe to some events
await wsExtension.Subscribe(new string[] {"/restapi/v1.0/account/~/extension/~/message-store"}, message =>
{
    Console.WriteLine(message); // just print the event message
});


// To detect OAuth related issues
// This is edge case. it should NOT happen in the first place
// If you encounter this issue, please review the logic of your app and fix it
// Possible reasons: access token expired or access token  revoked.
// You should realize that this should not happen at all.
// So you probably don't need the solution below. Instead, you need a better way to manage you token's lifecycle.
var reconnecting = false;
wsExtension.MessageReceived += async (sender, wsgMessage) =>
{
    if(!reconnecting && wsgMessage.meta.type == MessageType.Error && ((wsgMessage.body.errorCode as string)!).StartsWith("OAU-")) // some error regarding OAuth, like expired, invalid...etc.
    {
        reconnecting = true;
        Console.WriteLine("Token expired/revoked for some reason, I need to reconnect()");
        try
        {
            await rc.Refresh();
        }
        catch
        {
            await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
        }
        await wsExtension.Reconnect();
        reconnecting = false;
    }
};

// Check broken WebSocket connection
// normally this is not needed. But if there is network outage, you will need such code to handle it
// in case of network outage, the WebSocket connection will break. You will have to write code to reconnect
var bTimer = new System.Timers.Timer();
bTimer.Elapsed += new ElapsedEventHandler(async (object source, ElapsedEventArgs e) =>
{
    // Check if the connection has closed
    if (!reconnecting && !wsExtension.ws.IsRunning)
    {
        reconnecting = true;
        await wsExtension.Reconnect();
        reconnecting = false;
    }
});
bTimer.Interval = 60000; // 60 seconds
bTimer.Enabled = true;

// Trigger some notifications for testing purpose.
// This is optional, you may manually trigger events.
var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
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
    Console.WriteLine("Pager sent at " + DateTime.Now.ToString("yyyyMMdd HHmmss"));
}
