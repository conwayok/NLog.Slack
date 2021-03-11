using System;
using System.Collections.Generic;
using NLog.Common;
using NLog.Config;
using NLog.Slack.Dtos;
using NLog.Slack.Models;
using NLog.Targets;

namespace NLog.Slack
{
    [Target("Slack")]
    public class SlackTarget : TargetWithContext
    {
        [RequiredParameter] public string WebHookUrl { get; set; }

        public ProxySettings Proxy { get; set; }

        public bool Compact { get; set; }

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

            if (Proxy != null)
            {
                slack = SlackMessageBuilder.Build(WebHookUrl, Proxy)
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