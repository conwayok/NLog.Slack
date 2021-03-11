using System;
using NLog.Slack.Dtos;
using NLog.Slack.Models;

namespace NLog.Slack
{
    public class SlackMessageBuilder
    {
        private readonly string _webHookUrl;

        private readonly SlackClient _client;

        private readonly Payload _payload;

        public SlackMessageBuilder(string webHookUrl)
        {
            _webHookUrl = webHookUrl;
            _client = new SlackClient();
            _payload = new Payload();
        }

        public SlackMessageBuilder(
            string webHookUrl,
            ProxySettings proxySettings)
        {
            _webHookUrl = webHookUrl;
            _client = new SlackClient(proxySettings);
            _payload = new Payload();
        }

        public static SlackMessageBuilder Build(string webHookUrl)
        {
            return new SlackMessageBuilder(webHookUrl);
        }

        public static SlackMessageBuilder Build(
            string webHookUrl,
            ProxySettings proxySettings)
        {
            return new SlackMessageBuilder(webHookUrl, proxySettings);
        }

        public SlackMessageBuilder WithMessage(string message)
        {
            _payload.Text = message;

            return this;
        }

        public SlackMessageBuilder AddAttachment(Attachment attachment)
        {
            _payload.Attachments.Add(attachment);

            return this;
        }

        public SlackMessageBuilder OnError(Action<Exception> error)
        {
            _client.Error += error;

            return this;
        }

        public void Send()
        {
            _client.Send(_webHookUrl, _payload.ToJson());
        }
    }
}