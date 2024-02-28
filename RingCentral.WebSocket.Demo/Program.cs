using System.Timers;
using dotenv.net;
using RingCentral;
using RingCentral.Net.WebSocket;
using Timer = System.Timers.Timer;

var envVars = DotEnv.Read();

var rc = new RestClient(envVars["RINGCENTRAL_CLIENT_ID"], envVars["RINGCENTRAL_CLIENT_SECRET"],
    envVars["RINGCENTRAL_SERVER_URL"]);
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
var aTimer = new Timer();
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
await wsExtension.Subscribe(new string[]
{
    "/restapi/v1.0/account/~/extension/~/message-store"
}, message => { Console.WriteLine("Notification received"); });

// Check broken WebSocket connection
// In case of network outage, the WebSocket connection will break. You will have to write code to reconnect
// Special case: Server will disconnect you every 24 hours (absoluteTimeout), and it is handled by the SDK to reconnect. So it may not hit the logic below.
var bTimer = new Timer();
var reconnecting = false;
bTimer.Elapsed += new ElapsedEventHandler(async (object source, ElapsedEventArgs e) =>
{
    // Check if the connection has closed
    if (!reconnecting && !wsExtension.ws.IsRunning)
    {
        Console.WriteLine("Disconnection detected and reconnecting...");
        reconnecting = true;
        await wsExtension.Reconnect();
        reconnecting = false;
    }
});
bTimer.Interval = 10000; // 10 seconds
bTimer.Enabled = true;

var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
while (await timer.WaitForNextTickAsync())
{
    Console.WriteLine(DateTime.Now);
    Console.WriteLine("I am alive!");
}
