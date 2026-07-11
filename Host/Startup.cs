using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Host.Startup))]

namespace Host
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
