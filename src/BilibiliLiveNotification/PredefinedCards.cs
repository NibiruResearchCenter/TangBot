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

using System.Text;
using BilibiliLiveNotification.Model;
using DoDo.Open.Sdk.Models.Channels;
using DodoHosted.Base.Card;
using DodoHosted.Base.Card.BaseComponent;
using DodoHosted.Base.Card.CardComponent;
using DodoHosted.Base.Card.Enums;

namespace BilibiliLiveNotification;

public static class PredefinedCards
{
    public static CardMessage LiveStatusCard(SubscribedLiver liver)
    {
        var timeString = liver.CurrentStatus.IsLive
            ? string.Empty
            : GetTimeInterval(liver.CurrentStatus.StartTime, DateTimeOffset.UtcNow.AddHours(8));
        var timeStartToEndString = liver.CurrentStatus.IsLive
            ? $"{liver.CurrentStatus.StartTime:HH:mm} - Now"
            : $"{liver.CurrentStatus.StartTime:HH:mm} - {DateTimeOffset.UtcNow.AddHours(8):HH:mm}";

        return new CardMessage
        {
            Content = liver.CurrentStatus.IsLive
                ? $"***{liver.BiliUname}*** 正在直播 `{liver.CurrentStatus.Title}` 中！"
                : $"***{liver.BiliUname}*** 直播已结束",
            Card = new Card
            {
                Title = $"{liver.BiliUname} 直播通知",
                Theme = liver.CurrentStatus.IsLive ? CardTheme.Green : CardTheme.Red,
                Components = new List<ICardComponent>
                {
                    new Header(liver.CurrentStatus.Title),
                    new Image(liver.CurrentStatus.CoverFromDodo),
                    liver.CurrentStatus.IsLive
                        ? new TextWithModule(
                            "正在直播中！",
                            new Button(
                                "前往直播间",
                                new Uri($"https://live.bilibili.com/{liver.BiliLiveRoomId}"),
                                ButtonColor.Green),
                            TextWithModuleAlign.Right)
                        : new TextFiled($"直播已结束，共 {timeString}"),
                    new Remark(new Text(timeStartToEndString))
                }
            }
        };
    }

    public static CardMessage ApiStatusCard()
    {
        return new CardMessage(new Card
        {
            Title = "Bilibili API Request Status",
            Theme = CardTheme.Red,
            Components = new List<ICardComponent>
            {
                new MultilineText(new Text("Total Requests"), new Text(BiliApi.ApiRequestCount.ToString())),
                new MultilineText(new Text("Total Request Rate"), new Text(BiliApi.ApiRequestRate)),
                new MultilineText(new Text("Failed Requests"), new Text(BiliApi.FailedRequestCount.ToString())),
                new MultilineText(new Text("Failed Request Rate"), new Text(BiliApi.FailedRequestRate)),
                new MultilineText(new Text("Counting Period (hour)"), new Text(BiliApi.CountingTimespanHours)),
                new MultilineText(new Text("Counting Period (min)"), new Text(BiliApi.CountingTimespanMinutes))
            }
        });
    }

    public static CardMessage IslandSubscriptionCard(IEnumerable<SubscribedLiver> livers, GetChannelListOutput[] channels)
    {
        var card = new CardMessage(new Card
        {
            Title = "群组直播订阅列表", Theme = CardTheme.Green, Components = new List<ICardComponent>()
        });
        
        foreach (var liver in livers)
        {
            var msg = new StringBuilder();
            foreach (var channel in liver.NotifyChannels)
            {
                var c = channels.FirstOrDefault(x => x.ChannelId == channel.ChannelId);
                msg.AppendLine(c is null
                    ? $"<未知 {channel.ChannelId}>"
                    : $"{c.ChannelName}");
            }
            
            card.AddComponent(new MultilineText(
                new Text($"{liver.BiliUname}\n({liver.BiliUid})"),
                new Text(msg.ToString())));
        }

        return card;
    }
    
    private static string GetTimeInterval(DateTimeOffset start, DateTimeOffset end)
    {
        var timeSpan = end - start;
        var hour = (int)Math.Floor(timeSpan.TotalHours);
        var minute = (int)Math.Floor(timeSpan.TotalMinutes - hour * 60);

        return hour == 0 ? $"{minute} 分钟" : $"{hour} 小时 {minute} 分钟";
    }
}
