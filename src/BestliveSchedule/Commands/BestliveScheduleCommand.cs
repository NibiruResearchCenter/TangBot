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

using BestliveSchedule.Model;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Command;
using DodoHosted.Base.App.Context;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Open.Plugin;
using MongoDB.Driver;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeMadeStatic.Global

namespace BestliveSchedule.Commands;

public sealed class BestliveScheduleCommand : ICommandExecutor
{
    public async Task<bool> AddNewSubscription(
        CommandContext context,
        [Inject] IMongoCollection<CalendarSubscription> subscriptionCollection,
        [CmdOption("url", "u", "ICS 文件订阅地址")] string url,
        [CmdOption("name", "n", "主播名")] string name,
        [CmdOption("auth", "a", "HTTP Basic 鉴权字符串")] string authString,
        [CmdOption("tz", "t", "时区偏移", false)] int? tz,
        [CmdOption("force", "f", "强制添加，存在相同的将会被覆盖", false)] bool? force)
    {
        var subscription = await subscriptionCollection
            .Find(x => x.CalDavUrl == url)
            .FirstOrDefaultAsync();

        if (subscription is not null)
        {
            if (force is false or null)
            {
                await context.Reply.Invoke("已经存在相同的订阅了");
                return false;
            }
            
            await subscriptionCollection.DeleteOneAsync(x => x.CalDavUrl == url);
        }

        subscription = new CalendarSubscription
        {
            Name = name, CalDavUrl = url, BasicAuthString = authString, TimezoneOffset = tz ?? 0
        };
        
        await subscriptionCollection.InsertOneAsync(subscription);
        await context.Reply.Invoke("添加成功");
        return true;
    }

    public async Task<bool> GetDemoMessage(
        CommandContext context,
        [Inject] IMongoCollection<CalendarSubscription> subscriptionCollection,
        [Inject] IChannelLogger channelLogger,
        [CmdOption("date", "d", "日期")] DateOnly date)
    {
        var time = new DateTimeOffset(date.Year, date.Month, date.Day, 8, 0, 0, TimeSpan.FromHours(8));
        var subscriptions = await subscriptionCollection
            .Find(x => true)
            .ToListAsync();
        
        var events = new List<LiveEvent>();
        foreach (var subscription in subscriptions)
        {
            var liveEvents = await subscription.GetLiveEvents(time, channelLogger);
            events.AddRange(liveEvents);
        }

        var card = events.OrderBy(x => x.Time).ToArray().GetOhayoCard(time);
        await context.ReplyCard.Invoke(card);
        return true;
    }

    public CommandTreeBuilder GetBuilder()
    {
        return new CommandTreeBuilder("bestlive-schedule", "Bestlive 直播日历", "bestlive-schedule")
            .Then("add", "添加日历订阅", "add", AddNewSubscription)
            .Then("demo", "获取示例消息", "demo", GetDemoMessage);
    }
}
