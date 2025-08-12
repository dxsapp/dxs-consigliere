using System;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Websocket;

public interface IWebsocketClient: IDisposable
{
    bool IsRunning { get; }
    string Name { get; }
        
    TimeSpan IdleRestartTimeout { get; }
    DateTime LastIdleLogAt { get; set; }
    DateTime LastTickerAt { get; set; }
    TimeSpan ReconnectionDelay { get; }
            
    Task Start();
    Task Stop(string statusDescription = "Close");
    Task Restart(string statusDescription, TimeSpan delayBeforeStarting);
        
    void LogWsStatus();
    void ResetIdleTimer();
}