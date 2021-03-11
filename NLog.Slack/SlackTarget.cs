using System;
using System.Collections.Generic;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Slack.Dtos;
using NLog.Slack.Models;
using NLog.Targets;

namespace NLog.Slack
{
    [Target("Slack")]
    public class SlackTarget : TargetWithContext
    {
        [RequiredParameter] public string WebHookUrl { get; set; }

        public bool Compact { get; set; }

        public bool UseProxy { get; set; }
        public string ProxyHost { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUser { get; set; }
        public string ProxyPassword { get; set; }


        public override IList<TargetPropertyWithContext> ContextProperties { get; } =
            new List<TargetPropertyWithContext>();

        [ArrayParameter(typeof(TargetPropertyWithContext), "field")]
        public IList<TargetPropertyWithContext> Fields => ContextProperties;

        protected override void InitializeTarget()
        {
            if (String.IsNullOrWhiteSpace(WebHookUrl))
                throw new ArgumentOutOfRangeException("WebHookUrl", "Webhook URL cannot be empty.");

            Uri uriResult;
            if (!Uri.TryCreate(WebHookUrl, UriKind.Absolute, out uriResult))
                throw new ArgumentOutOfRangeException("WebHookUrl", "Webhook URL is an invalid URL.");

            if (!Compact && ContextProperties.Count == 0)
            {
                ContextProperties.Add(new TargetPropertyWithContext("Process Name",
                    Layout = "${machinename}\\${processname}"));
                ContextProperties.Add(new TargetPropertyWithContext("Process PID", Layout = "${processid}"));
            }

            // validate proxy settings
            if (UseProxy)
            {
                if (string.IsNullOrEmpty(ProxyHost))
                    throw new ArgumentOutOfRangeException("ProxyHost", "ProxyHost must not be empty");

                if (ProxyPort == 0)
                    throw new ArgumentOutOfRangeException("ProxyPort", "ProxyPort must not be empty");

                if (!string.IsNullOrEmpty(ProxyUser) && string.IsNullOrEmpty(ProxyPassword))
                    throw new ArgumentOutOfRangeException("ProxyUser", "ProxyUser configured with no ProxyPassword");

                if (!string.IsNullOrEmpty(ProxyPassword) && string.IsNullOrEmpty(ProxyUser))
                    throw new ArgumentOutOfRangeException("ProxyPassword",
                        "ProxyPassword configured with no ProxyUser");
            }


            base.InitializeTarget();
        }

        protected override void Write(AsyncLogEventInfo info)
        {
            try
            {
                SendToSlack(info);
                info.Continuation(null);
            }
            catch (Exception e)
            {
                info.Continuation(e);
            }
        }

        private void SendToSlack(AsyncLogEventInfo info)
        {
            var message = RenderLogEvent(Layout, info.LogEvent);

            SlackMessageBuilder slack;

            if (UseProxy)
            {
                var proxy = new ProxySettings
                {
                    Host = ProxyHost, Port = ProxyPort, User = ProxyUser, Password = ProxyPassword
                };

                slack = SlackMessageBuilder.Build(WebHookUrl, proxy)
                    .OnError(e => info.Continuation(e))
                    .WithMessage(message);
            }
            else
            {
                slack = SlackMessageBuilder
                    .Build(WebHookUrl)
                    .OnError(e => info.Continuation(e))
                    .WithMessage(message);
            }


            if (ShouldIncludeProperties(info.LogEvent) || ContextProperties.Count > 0)
            {
                var color = GetSlackColorFromLogLevel(info.LogEvent.Level);
                Attachment attachment = new Attachment(info.LogEvent.Message) {Color = color};
                var allProperties = GetAllProperties(info.LogEvent);
                foreach (var property in allProperties)
                {
                    if (string.IsNullOrEmpty(property.Key))
                        continue;

                    var propertyValue = property.Value?.ToString();
                    if (string.IsNullOrEmpty(propertyValue))
                        continue;

                    attachment.Fields.Add(new Field(property.Key) {Value = propertyValue, Short = true});
                }

                if (attachment.Fields.Count > 0)
                    slack.AddAttachment(attachment);
            }

            var exception = info.LogEvent.Exception;
            if (!Compact && exception != null)
            {
                var color = GetSlackColorFromLogLevel(info.LogEvent.Level);
                var exceptionAttachment = new Attachment(exception.Message) {Color = color};
                exceptionAttachment.Fields.Add(new Field("StackTrace")
                {
                    Title = $"Type: {exception.GetType()}",
                    Value = exception.StackTrace ?? "N/A"
                });

                slack.AddAttachment(exceptionAttachment);
            }

            slack.Send();
        }

        private string GetSlackColorFromLogLevel(LogLevel level)
        {
            if (LogLevelSlackColorMap.TryGetValue(level, out var color))
                return color;
            return "#cccccc";
        }

        private static readonly Dictionary<LogLevel, string> LogLevelSlackColorMap = new Dictionary<LogLevel, string>
        {
            {LogLevel.Warn, "warning"},
            {LogLevel.Error, "danger"},
            {LogLevel.Fatal, "danger"},
            {LogLevel.Info, "#2a80b9"},
        };
    }
}