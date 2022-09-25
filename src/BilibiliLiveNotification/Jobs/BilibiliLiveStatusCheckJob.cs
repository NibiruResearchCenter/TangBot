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
using DoDo.Open.Sdk.Models.Messages;
using DoDo.Open.Sdk.Models.Resources;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.Card;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf

namespace BilibiliLiveNotification.Jobs;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class BilibiliLiveStatusCheckJob : IPluginHostedService
{
    private readonly IMongoCollection<SubscribedLiver> _subscribedLiverCollection;
    private readonly ILogger<BilibiliLiveStatusCheckJob> _logger;
    private readonly IChannelLogger _channelLogger;
    private readonly OpenApiService _openApiService;
    private readonly PluginConfigurationManager _pluginConfigurationManager;

    public BilibiliLiveStatusCheckJob(
        [Inject] IMongoCollection<SubscribedLiver> subscribedLiverCollection,
        [Inject] ILogger<BilibiliLiveStatusCheckJob> logger,
        [Inject] IChannelLogger channelLogger,
        [Inject] OpenApiService openApiService,
        [Inject] PluginConfigurationManager pluginConfigurationManager)
    {
        _subscribedLiverCollection = subscribedLiverCollection;
        _logger = logger;
        _channelLogger = channelLogger;
        _openApiService = openApiService;
        _pluginConfigurationManager = pluginConfigurationManager;
    }

    private static TimeSpan s_delayTime = TimeSpan.FromSeconds(120);
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                var livers = await _subscribedLiverCollection
                    .Find(_ => true)
                    .ToListAsync(cancellationToken);

                var checkResults = livers.Count == 0
                    ? Enumerable.Empty<BiliApi.LiveCheckResult>()
                    : await BiliApi.GetLiveStatus(livers, cancellationToken);

                foreach (var result in checkResults)
                {
                    try
                    {
                        var liver = livers.First(x => x.BiliUid == result.BiliUid);

                        // 未开播 -> 未开播
                        if (liver.CurrentStatus.IsLive == false && result.IsLive == false)
                        {
                            continue;
                        }

                        // 开播 -> 开播
                        if (liver.CurrentStatus.IsLive && result.IsLive)
                        {
                            continue;
                        }

                        // 未开播 -> 开播
                        if (liver.CurrentStatus.IsLive == false && result.IsLive)
                        {
                            var resource =
                                await _openApiService.SetResourcePictureUploadAsync(
                                    new SetResourceUploadInput { FilePath = result.Cover }, true)!;

                            liver.CurrentStatus.Title = result.Title;
                            liver.CurrentStatus.Cover = result.Cover;
                            liver.CurrentStatus.CoverFromDodo = resource.Url;
                            liver.CurrentStatus.IsLive = true;
                            liver.CurrentStatus.StartTime = result.StartTime;
                            liver.CurrentStatus.MessageIds = new List<string>();

                            var card = PredefinedCards.LiveStatusCard(liver);

                            foreach (var notifyChannel in liver.NotifyChannels)
                            {
                                var response = await _openApiService.SetChannelMessageSendAsync(
                                    new SetChannelMessageSendInput<MessageBodyCard>
                                    {
                                        ChannelId = notifyChannel.ChannelId, MessageBody = card.Serialize()
                                    }, true);

                                liver.CurrentStatus.MessageIds.Add(response.MessageId);
                            }

                            await _subscribedLiverCollection.FindOneAndReplaceAsync(x => x.Id == liver.Id, liver,
                                cancellationToken: cancellationToken);

                            continue;
                        }

                        // 开播 -> 未开播
                        if (liver.CurrentStatus.IsLive && result.IsLive == false)
                        {
                            liver.CurrentStatus.IsLive = false;

                            var card = PredefinedCards.LiveStatusCard(liver);

                            foreach (var msg in liver.CurrentStatus.MessageIds)
                            {
                                await _openApiService.SetChannelMessageEditAsync(
                                    new SetChannelMessageEditInput<MessageBodyCard>
                                    {
                                        MessageId = msg, MessageBody = card.Serialize()
                                    }, true);
                            }

                            await _subscribedLiverCollection.FindOneAndReplaceAsync(x => x.Id == liver.Id, liver,
                                cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _channelLogger.LogError(
                            HostEnvs.DodoHostedAdminIsland,
                            "Bilibili 直播检查状态出现问题，" +
                            $"BiliUID：`{result.BiliUid}`" +
                            $"Type：`{ex.GetType().Name}`，" +
                            $"Exception：`{ex.Message}\n`" +
                            ex.StackTrace);
                    }
                }

                var conf = await _pluginConfigurationManager.GetObjectValue<PluginConfiguration>("config");
                s_delayTime = TimeSpan.FromSeconds(conf.JobRunningInterval);

                _logger.LogDebug("Bilibili 直播检查状态完成，等待 {delayTime} 后再次检查", s_delayTime);
            }
            catch (Exception ex)
            {
                await _channelLogger.LogError(
                    HostEnvs.DodoHostedAdminIsland,
                    "BilibiliLiveStatusCheckJob 出现问题，" +
                    $"Type：`{ex.GetType().Name}`，" +
                    $"Exception：`{ex.Message}\n`" +
                    ex.StackTrace);
            }
            finally
            {
                await Task.Delay(s_delayTime, cancellationToken);
            }
        }
    }

    public string HostedServiceName => "BilibiliLiveStatusCheckJob";
}
