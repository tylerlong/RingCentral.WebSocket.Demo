using System.Timers;
using RingCentral;
using dotenv.net;
using RingCentral.Net.WebSocket;

var envVars = DotEnv.Read();

var rc = new RestClient(envVars["RINGCENTRAL_CLIENT_ID"], envVars["RINGCENTRAL_CLIENT_SECRET"], envVars["RINGCENTRAL_SERVER_URL"]);
await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);

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
    catch
    {
        await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
    }
});
aTimer.Interval = 1800000; // 30 minutes
aTimer.Enabled = true;

// install the WebSocket extension
var wsExtension = new WebSocketExtension(new WebSocketOptions
{
    debugMode = true // enable this will print all the WebSocket messages
});
await rc.InstallExtension(wsExtension);

// subscribe to some events
await wsExtension.Subscribe(new string[] {"/restapi/v1.0/account/~/extension/~/message-store"}, message =>
{
    Console.WriteLine(message); // just print the event message
});


// to detect OAuth related issues
// This may not be necessary, most apps don't need this
// There is a 5 active oauth sessions per user per app limit
// If for some particular reasons you need to run 5+ instances, you will need the following code to recover your app
// You will need it if you often generate 5+ tokens in a short time (maybe for testing purpose?), existing tokens maybe revoked upon 5+ new tokens generated
// But you really shouldn't run 5+ instances of your app. There is no way to keep them all up and running. This is just a workaround to recover your current instance. 
var reconnecting = false;
wsExtension.RawMessageReceived += async (sender, str) =>
{
    if(!reconnecting && str.Contains("\"OAU-")) // some error regarding OAuth, like expired, invalid...etc.
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
    // the line below is for testing only, your app doesn't need it
    // during testing, I create tons of new tokens and existing tokens keep being revoked
    // so before invoking any API call I need to generate a new token
    // this is so special case for testing, your app doesn't need it!!!
    await rc.Authorize(envVars["RINGCENTRAL_JWT_TOKEN"]);
    
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
