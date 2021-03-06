﻿using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using NLog.Fluent;
using UAParser;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(40)]
    public class RequestInfoPlugin : EventProcessorPluginBase {
        public const int MAX_VALUE_LENGTH = 1000;
        public static readonly List<string> DefaultExclusions = new List<string> {
            "*VIEWSTATE*",
            "*EVENTVALIDATION*",
            "*ASPX*",
            "__RequestVerificationToken",
            "ASP.NET_SessionId",
            "__LastErrorId",
            "WAWebSiteID",
            "ARRAffinity"
        };

        public override void EventProcessing(EventContext context) {
            var request = context.Event.GetRequestInfo();
            if (request == null)
                return;

            var exclusions = context.Project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.DataExclusions)
                    ? DefaultExclusions.Union(context.Project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.DataExclusions)).ToList()
                    : DefaultExclusions;

            if (!String.IsNullOrEmpty(request.UserAgent)) {
                try {
                    var info = Parser.GetDefault().Parse(request.UserAgent);
                    request.Data[RequestInfo.KnownDataKeys.Browser] = info.UserAgent.Family;
                    if (!String.IsNullOrEmpty(info.UserAgent.Major)) {
                        request.Data[RequestInfo.KnownDataKeys.BrowserVersion] = info.UserAgent.ToString();
                        request.Data[RequestInfo.KnownDataKeys.BrowserMajorVersion] = info.UserAgent.Family + " " + info.UserAgent.Major;
                    }

                    request.Data[RequestInfo.KnownDataKeys.Device] = info.Device.Family;

                    request.Data[RequestInfo.KnownDataKeys.OS] = info.OS.Family;
                    if (!String.IsNullOrEmpty(info.OS.Major)) {
                        request.Data[RequestInfo.KnownDataKeys.OSVersion] = info.OS.ToString();
                        request.Data[RequestInfo.KnownDataKeys.OSMajorVersion] = info.OS.Family + " " + info.OS.Major;
                    }

                    var botPatterns = context.Project.Configuration.Settings.ContainsKey(SettingsDictionary.KnownKeys.UserAgentBotPatterns)
                      ? context.Project.Configuration.Settings.GetStringCollection(SettingsDictionary.KnownKeys.UserAgentBotPatterns).ToList()
                      : new List<string>();

                    request.Data[RequestInfo.KnownDataKeys.IsBot] = info.Device.IsSpider || request.UserAgent.AnyWildcardMatches(botPatterns);
                } catch (Exception ex) {
                    Log.Warn().Project(context.Event.ProjectId).Message("Unable to parse user agent {0}. Exception: {1}", request.UserAgent, ex.Message).Write();
                }
            }

            context.Event.AddRequestInfo(request.ApplyDataExclusions(exclusions, MAX_VALUE_LENGTH));
        }
    }
}