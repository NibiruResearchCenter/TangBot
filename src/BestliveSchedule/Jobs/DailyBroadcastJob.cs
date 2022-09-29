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
using DoDo.Open.Sdk.Models.Channels;
using DoDo.Open.Sdk.Models.Messages;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.Card;
using DodoHosted.Open.Plugin;
using MongoDB.Driver;

namespace BestliveSchedule.Jobs;

public sealed class DailyBroadcastJob : IPluginHostedService
{
    private readonly IMongoCollection<CalendarSubscription> _subscriptionCollection;
    private readonly OpenApiService _openApiService;
    private readonly PluginConfigurationManager _pluginConfigurationManager;
    private readonly IChannelLogger _channelLogger;

    public DailyBroadcastJob(
        [Inject] IMongoCollection<CalendarSubscription> subscriptionCollection,
        [Inject] OpenApiService openApiService,
        [Inject] PluginConfigurationManager pluginConfigurationManager,
        [Inject] IChannelLogger channelLogger)
    {
        _subscriptionCollection = subscriptionCollection;
        _openApiService = openApiService;
        _pluginConfigurationManager = pluginConfigurationManager;
        _channelLogger = channelLogger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested is false)
        {
            var now = DateTimeOffset.UtcNow.AddHours(8);
            if (now.Hour != 8 && now.Minute != 00)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                continue;
            }

            try
            {
                var conf = await _pluginConfigurationManager.GetObjectValue<PluginConfiguration>("config");
                
                var subscriptions = await _subscriptionCollection
                    .Find(x => true)
                    .ToListAsync(cancellationToken);

                var events = new List<LiveEvent>();
                foreach (var subscription in subscriptions)
                {
                    var liveEvents = await subscription.GetLiveEvents(now, _channelLogger);
                    events.AddRange(liveEvents);
                }

                var card = events.OrderBy(x => x.Time).ToArray().GetOhayoCard(now);
                await _openApiService.SetChannelMessageSendAsync(new SetChannelMessageSendInput<MessageBodyCard>
                    {
                        ChannelId = conf.SendChannel, MessageBody = card.Serialize()
                    }, true);

                await Task.Delay(TimeSpan.FromHours(23), cancellationToken);
            }
            catch(Exception ex)
            {
                await _channelLogger.LogError(HostEnvs.DodoHostedAdminIsland,
                    "Daily Broadcast Job Error，" +
                    $"Type：{ex.GetType().Name}，" +
                    $"Message：{ex.Message}");
            }
        }
    }

    public string HostedServiceName => "DailyBroadcastJob";
}
