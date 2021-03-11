using System;
using System.Net;
using System.Text;
using NLog.Slack.Dtos;

namespace NLog.Slack
{
    public class SlackClient
    {
        public event Action<Exception> Error;

        private readonly ProxySettings _proxySettings;


        public SlackClient()
        {
        }

        public SlackClient(ProxySettings proxySettings)
        {
            _proxySettings = proxySettings;
        }


        public void Send(string url, string data)
        {
            try
            {
                using (var client = new WebClient())
                {
                    if (_proxySettings != null)
                    {
                        client.Proxy = new WebProxy(_proxySettings.Host, _proxySettings.Port);
                        if (_proxySettings.User != null && _proxySettings.Password != null)
                        {
                            client.Proxy.Credentials =
                                new NetworkCredential(_proxySettings.User, _proxySettings.Password);
                        }

                        Console.WriteLine("my ip for slack is {0}",
                            client.DownloadString("http://bot.whatismyipaddress.com/"));
                    }

                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.Encoding = Encoding.UTF8;
                    client.UploadString(url, "POST", data);
                }
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        private void OnError(Exception obj)
        {
            if (Error != null)
                Error(obj);
        }
    }
}