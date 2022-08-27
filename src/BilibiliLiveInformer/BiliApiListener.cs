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

using BilibiliLiveInformer.Entity;
using DoDo.Open.Sdk.Models.Channels;
using DoDo.Open.Sdk.Models.Messages;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BilibiliLiveInformer;

public class BiliApiListener : IPluginHostedService
{
    public string HostedServiceName => "BiliApiListener";

    public async Task StartAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken cancellationToken)
    {
        var channelLogger = serviceProvider.GetRequiredService<IChannelLogger>();
        var openApi = serviceProvider.GetRequiredService<OpenApiService>();
        var mongoDb = serviceProvider.GetRequiredService<IMongoDatabase>();
        var subscribedLiverCollection = mongoDb.GetCollection<SubscribedLiver>(Constants.MONGO_SUBSCRIBED_LIVER_COLLECTION);
        var currentStatusCollection = mongoDb.GetCollection<CurrentStatus>(Constants.MONGO_CURRENT_STATUS_COLLECTION);
        
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                var livers = await subscribedLiverCollection.AsQueryable().ToListAsync(cancellationToken);

                foreach (var liver in livers)
                {
                    var result = await BiliApi.GetLiveStatus(liver.BiliUid, cancellationToken);
                    logger.LogTrace("UID {TraceBiliUid}({TraceBiliUname}) BiliApiRequestResult: {TraceBiliApiRequestResult}",
                        liver.BiliUid, liver.BiliUname, result);

                    var current = await currentStatusCollection
                        .Find(x => x.BiliUid == liver.BiliUid)
                        .FirstOrDefaultAsync(cancellationToken);

                    var message = string.Empty;
                    
                    if (current is null)
                    {
                        await currentStatusCollection.InsertOneAsync(new CurrentStatus
                        {
                            BiliUid = liver.BiliUid, IsLive = result.IsLive, StartTime = DateTimeOffset.UtcNow
                        }, cancellationToken: cancellationToken);

                        logger.LogDebug("Current Status for UID {TraceBiliUid}({TraceBiliUname}) is null, add new.",
                            liver.BiliUid, liver.BiliUname);

                        if (result.IsLive)
                        {
                            message =
                                $"**{liver.BiliUname}** 正在直播 `{result.Title}` [前往直播间](https://live.bilibili.com/{liver.BiliLiveRoomId})";
                        }
                    }
                    else
                    {
                        logger.LogDebug("UID {TraceBiliUid}({TraceBiliUname}) current status is {TraceCurrentIsLive}, record status is {TraceRecordIsLive}",
                            liver.BiliUid, liver.BiliUname, result.IsLive, current.IsLive);

                        switch (current.IsLive)
                        {
                            // 记录在不在播，查询结果在播 => 开播了
                            case false when result.IsLive:
                                current.IsLive = true;
                                current.StartTime = DateTimeOffset.UtcNow;
                                message = $"**{liver.BiliUname}** 正在直播 `{result.Title}` [前往直播间](https://live.bilibili.com/{liver.BiliLiveRoomId})";
                                break;
                            // 记录在播，查询结果不在播 => 下播了
                            case true when result.IsLive is false:
                                current.IsLive = false;
                                var duration = DateTimeOffset.UtcNow - current.StartTime;
                                var realHours = Convert.ToInt32(Math.Floor(duration.TotalHours));
                                message = $"**{liver.BiliUname}** 下播啦！本次直播时长 `{realHours}` 小时 `{duration.Minutes}` 分钟！";
                                break;
                        }

                        if (string.IsNullOrEmpty(message) is false)
                        {
                            await currentStatusCollection
                                .FindOneAndReplaceAsync(x => x.Id == current.Id, current, cancellationToken: cancellationToken);
                        }
                    }

                    if (string.IsNullOrEmpty(message) is false)
                    {
                        foreach (var channel in liver.NotifyChannels)
                        {
                            await openApi.SetChannelMessageSendAsync(new SetChannelMessageSendInput<MessageBodyText>
                            {
                                ChannelId = channel.ChannelId,
                                MessageBody = new MessageBodyText { Content = message }
                            }, true);
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                await channelLogger.LogError(HostEnvs.DodoHostedAdminIsland,
                    $"BiliApiListener 发生错误: `{e.GetType().FullName}` {e.Message}\n{e.StackTrace}");
            }
        }
    }
    
    public record ExecutionResult(bool IsLive, string Title);
}
