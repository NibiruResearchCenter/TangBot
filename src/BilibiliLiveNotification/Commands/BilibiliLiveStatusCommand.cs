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

using BilibiliLiveNotification.Model;
using DoDo.Open.Sdk.Models.Channels;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Command;
using DodoHosted.Base.App.Context;
using DodoHosted.Base.App.Types;
using DodoHosted.Open.Plugin;
using MongoDB.Driver;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeMadeStatic.Global

namespace BilibiliLiveNotification.Commands;

public sealed class BilibiliLiveStatusCommand : ICommandExecutor
{
    public async Task<bool> AddSubscription(
        CommandContext context,
        [Inject] IMongoCollection<SubscribedLiver> collection, 
        [CmdOption("id", "i", "主播 B 站 UID")] string uid,
        [CmdOption("channel", "c", "订阅频道")] DodoChannelId channel)
    {
        var subscribedLiver = await collection
            .Find(x => x.BiliUid == uid)
            .FirstOrDefaultAsync() ?? await BiliApi.GetLiverInfo(uid, CancellationToken.None);

        if (subscribedLiver.NotifyChannels.Any(x => x.ChannelId == channel.Value))
        {
            await context.Reply.Invoke($"已在该频道存在对主播 `{subscribedLiver.BiliUname}` ({subscribedLiver.BiliUid}) 订阅");
            return false;
        }
        
        subscribedLiver.NotifyChannels.Add(new NotifyChannel
        {
            IslandId = context.EventInfo.IslandId,
            ChannelId = channel.Value
        });
        
        await collection.ReplaceOneAsync(x => x.BiliUid == uid, subscribedLiver, new ReplaceOptions
        {
            IsUpsert = true
        });
        
        await context.Reply.Invoke($"已在频道 {channel.Ref} 添加对主播 `{subscribedLiver.BiliUname}` ({subscribedLiver.BiliUid}) 的订阅");
        return true;
    }

    public async Task<bool> RemoveSubscription(
        CommandContext context,
        [Inject] IMongoCollection<SubscribedLiver> collection, 
        [CmdOption("id", "i", "主播 B 站 UID")] string uid,
        [CmdOption("channel", "c", "订阅频道")] DodoChannelId channel)
    {
        var subscribedLiver = await collection
            .Find(x => x.BiliUid == uid)
            .FirstOrDefaultAsync();

        if (subscribedLiver is null)
        {
            await context.Reply.Invoke("未找到该主播的订阅信息");
            return false;
        }

        if (subscribedLiver.NotifyChannels.Any(x => x.ChannelId == channel.Value) is false)
        {
            await context.Reply.Invoke($"未找到频道 {channel.Ref} 对主播 `{subscribedLiver.BiliUname}` ({subscribedLiver.BiliUid}) 的订阅信息");
            return false;
        }

        subscribedLiver.NotifyChannels.RemoveAll(x => x.ChannelId == channel.Value);
        
        if (subscribedLiver.NotifyChannels.Count == 0)
        {
            await collection.FindOneAndDeleteAsync(x => x.Id == subscribedLiver.Id);
        }
        else
        {
            await collection.ReplaceOneAsync(x => x.Id == subscribedLiver.Id, subscribedLiver);
        }
        
        await context.Reply.Invoke($"已在频道 {channel.Ref} 移除对主播 `{subscribedLiver.BiliUname}` ({subscribedLiver.BiliUid}) 的订阅");
        return true;
    }

    public async Task<bool> ListSubscriptions(
        CommandContext context,
        [Inject] IMongoCollection<SubscribedLiver> collection,
        [Inject] OpenApiService openApiService)
    {
        var subscribedLivers = await collection
            .Find(_ => true)
            .ToListAsync();

        var islandSubscribedLivers = subscribedLivers
            .Where(x => x.NotifyChannels.Any(y => y.IslandId == context.EventInfo.IslandId))
            .ToList();
        
        if (islandSubscribedLivers.Count == 0)
        {
            await context.Reply.Invoke("当前群组没有订阅任何主播");
            return false;
        }

        var channelList = await openApiService.GetChannelListAsync(new GetChannelListInput
        {
            IslandId = context.EventInfo.IslandId
        });

        var card = PredefinedCards.IslandSubscriptionCard(islandSubscribedLivers, channelList.ToArray());
        await context.ReplyCard.Invoke(card);
        return true;
    }

    public async Task<bool> GetBiliApiStatus(
        CommandContext context)
    {
        var card = PredefinedCards.ApiStatusCard();
        await context.ReplyCard.Invoke(card);
        return true;
    }

    public CommandTreeBuilder GetBuilder()
    {
        return new CommandTreeBuilder("bili-live", "Bilibili 直播通知", "bili-live")
            .Then("add", "添加订阅", "modify", AddSubscription)
            .Then("remove", "移除订阅", "modify", RemoveSubscription)
            .Then("status", "获取 Bilibili API 请求状态", "status", GetBiliApiStatus)
            .Then("list", "列出群组订阅", "list", ListSubscriptions);
    }
}
