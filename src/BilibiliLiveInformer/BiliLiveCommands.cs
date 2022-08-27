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
using BilibiliLiveInformer.Entity;
using DodoHosted.Base;
using DodoHosted.Base.App.Helpers;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.App.Models;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace BilibiliLiveInformer;

public class BiliLiveCommands : ICommandExecutor
{
    public async Task<CommandExecutionResult> Execute(
        string[] args,
        CommandMessage message,
        IServiceProvider provider,
        IPermissionManager permissionManager,
        PluginBase.Reply reply,
        bool shouldAllow = false)
    {
        if (shouldAllow is false)
        {
            if (await permissionManager.CheckPermission("blive-informer.local", message) is false)
            {
                return CommandExecutionResult.Unauthorized;
            }
        }

        var mongo = provider.GetRequiredService<IMongoDatabase>();
        var subCollection = mongo.GetCollection<SubscribedLiver>(Constants.MONGO_SUBSCRIBED_LIVER_COLLECTION);
        var curCollection = mongo.GetCollection<CurrentStatus>(Constants.MONGO_CURRENT_STATUS_COLLECTION);

        return args switch
        {
            [_, "add", var biliUid, var channel] =>
                await RunAdd(biliUid, channel, message.IslandId, subCollection, reply),
            [_, "remove", var biliUid, var channel] =>
                await RunRemove(biliUid, channel, message.IslandId, subCollection, curCollection, reply),
            [_, "list"] =>
                await RunList(message.IslandId, subCollection, reply),
            [_, "statistic"] =>
                await RunStatistic(reply),
            _ => CommandExecutionResult.Unknown
        };
    }

    public CommandMetadata GetMetadata() => new(
        CommandName: "blive-informer",
        Description: "Bilibili 直播推送",
        HelpText: @"""
- `{{PREFIX}}blive-informer add <Bilibili UID> <频道 ID/#频道>`    添加一个监听
- `{{PREFIX}}blive-informer remove <Bilibili UID> <频道 ID/#频道/all>`    移除一个监听
- `{{PREFIX}}blive-informer list`    查看所有监听的直播
- `{{PREFIX}}blive-informer statistic`    查看 API 调用统计
""",
        PermissionNodes: new Dictionary<string, string>
        {
            { "blive-informer.local", "允许使用 blive-informer 指令" },
        });

    private static async Task<CommandExecutionResult> RunAdd(
        string biliUid, string channel, string island,
        IMongoCollection<SubscribedLiver> collection,
        PluginBase.Reply reply)
    {
        var channelId = channel.ExtractChannelId();
        if (channelId is null)
        {
            await reply.Invoke("频道 ID 格式错误");
            return CommandExecutionResult.Failed;
        }
        
        var existedSubscription = await collection.Find(x => x.BiliUid == biliUid).FirstOrDefaultAsync();

        if (existedSubscription is null)
        {
            var (newSubscription, exception) = await BiliApi.GetLiverInfo(biliUid);
            if (exception is not null)
            {
                await reply.Invoke($"发生 API 错误： `{exception.GetType().FullName}` {exception.Message}");
                return CommandExecutionResult.Failed;
            }
            
            newSubscription!.NotifyChannels.Add(new NotifyChannel{ IslandId = island, ChannelId = channelId });
            await collection.InsertOneAsync(newSubscription);
        }
        else
        {
            var contains = existedSubscription.NotifyChannels.Contains(new NotifyChannel { IslandId = island, ChannelId = channelId });
            if (contains)
            {
                await reply.Invoke($"该频道已存在对 {existedSubscription.BiliUid}({existedSubscription.BiliUname}) 的监听");
                return CommandExecutionResult.Failed;
            }
            
            existedSubscription.NotifyChannels.Add(new NotifyChannel { IslandId = island, ChannelId = channelId });
            await collection.ReplaceOneAsync(x => x.Id == existedSubscription.Id, existedSubscription);
        }

        await reply.Invoke("添加成功");
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> RunRemove(
        string biliUid, string channel, string island,
        IMongoCollection<SubscribedLiver> subCollection,
        IMongoCollection<CurrentStatus> curCollection,
        PluginBase.Reply reply)
    {
        var channelId = channel is "all" ? "ALL" : channel.ExtractChannelId();
        if (channelId is null)
        {
            await reply.Invoke("频道 ID 格式错误");
            return CommandExecutionResult.Failed;
        }

        var existedSubscription = await subCollection.Find(x => x.BiliUid == biliUid).FirstOrDefaultAsync();
        if (existedSubscription is null)
        {
            await reply.Invoke($"未找到对 {biliUid} 的监听");
            return CommandExecutionResult.Failed;
        }

        if (channelId is not "ALL")
        {
            existedSubscription.NotifyChannels.RemoveAll(x => x.IslandId == island && x.ChannelId == channelId);
        }
        else
        {
            existedSubscription.NotifyChannels.RemoveAll(x => x.IslandId == island);
        }
        
        if (existedSubscription.NotifyChannels.Count == 0)
        {
            await curCollection.DeleteOneAsync(x => x.BiliUid == existedSubscription.BiliUid);
            await subCollection.DeleteOneAsync(x => x.Id == existedSubscription.Id);
        }
        else
        {
            await subCollection.ReplaceOneAsync(x => x.Id == existedSubscription.Id, existedSubscription);
        }
        
        await reply.Invoke("移除成功");
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> RunList(
        string island,
        IMongoCollection<SubscribedLiver> collection,
        PluginBase.Reply reply)
    {
        var subscriptions = await collection
            .Find(x => x.NotifyChannels.Any(y => y.IslandId == island))
            .ToListAsync();

        var message = new StringBuilder();
        if (subscriptions.Count != 0)
        {
            foreach (var subscription in subscriptions)
            {
                var channels = subscription.NotifyChannels.Where(x => x.IslandId == island);
                message.AppendLine($"- {subscription.BiliUname} `{subscription.BiliUid}`");
                foreach (var channel in channels)
                {
                    message.AppendLine($"  $ <#{channel.ChannelId}>");
                }
            }
        }
        else
        {
            message.AppendLine("没有监听的直播");
        }

        await reply.Invoke(message.ToString());
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> RunStatistic(PluginBase.Reply reply)
    {
        await reply.Invoke($"API Request Rate: {BiliApi.ApiRequestRate}\nAPI Failed Request Rate: {BiliApi.FailedRequestRate}");
        return CommandExecutionResult.Success;
    }
}
