// This file is a part of TangBot project.
// 
// Copyright (C) 2022 NibiruResearchCenter and all Contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using System.Net.Http.Headers;
using System.Text;
using BestliveSchedule.Model;
using DodoHosted.Base.App;
using DodoHosted.Base.App.Interfaces;
using Ical.Net;

namespace BestliveSchedule;

public static class CalDavApi
{
    private static readonly HttpClient s_client = new();

    public static async Task<IEnumerable<LiveEvent>> GetLiveEvents(
        this CalendarSubscription subscription,
        DateTimeOffset broadcastTime,
        IChannelLogger logger)
    {
        try
        {
            var base64AuthString = Convert.ToBase64String(Encoding.ASCII.GetBytes(subscription.BasicAuthString));

            var request = new HttpRequestMessage(HttpMethod.Get, subscription.CalDavUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64AuthString);

            var response = await s_client.SendAsync(request);

            var responseStream = await response.Content.ReadAsStreamAsync();
            var cal = Calendar.Load(responseStream);

            var calEvent = cal.Events.Where(x =>
                x.Start.AsDateTimeOffset.AddHours(subscription.TimezoneOffset) >= broadcastTime &&
                x.Start.AsDateTimeOffset.AddHours(subscription.TimezoneOffset) <= broadcastTime.AddDays(1));

            var liveEvents = calEvent
                .Select(x => new LiveEvent(
                    FormatTimeString(
                        x.Start.AsDateTimeOffset.AddHours(subscription.TimezoneOffset),
                        x.End.AsDateTimeOffset.AddHours(subscription.TimezoneOffset)),
                    subscription.Name,
                    x.Summary));

            return liveEvents;
        }
        catch (Exception ex)
        {
            await logger.LogError(HostEnvs.DodoHostedAdminIsland,
                $"获取 `{subscription.Name}` 的直播日历失败，" +
                $"Exception: {ex.GetType().Name}，" +
                $"Message：{ex.Message}");
        }
        
        return Enumerable.Empty<LiveEvent>();
    }

    private static string FormatTimeString(DateTimeOffset start, DateTimeOffset end)
    {
        return start.AddHours(2).AddMinutes(5) == end
            ? $"{start.Hour:00}:{start.Minute:00} - ??:??"
            : $"{start.Hour:00}:{start.Minute:00} - {end.Hour:00}:{end.Minute:00}";
    }
}
