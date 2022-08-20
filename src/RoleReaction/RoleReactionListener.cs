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

using DoDo.Open.Sdk.Models.Members;
using DoDo.Open.Sdk.Models.Roles;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.Events;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RoleReaction.Model;

namespace RoleReaction;

public class RoleReactionListener : IDodoHostedPluginEventHandler<DodoMessageReactionEvent>
{
    public async Task Handle(DodoMessageReactionEvent @event, IServiceProvider provider, ILogger logger)
    {
        var collection = provider.GetRequiredService<IMongoDatabase>().GetCollection<ReactionMessage>("tb-rr-messages");
        var msgId = @event.Message.Data.EventBody.ReactionTarget.Id;
        var emoji = @event.Message.Data.EventBody.ReactionEmoji.Id;
        var isAdd = @event.Message.Data.EventBody.ReactionType == 1;
        
        var message = await collection
            .Find(x => x.MessageId == msgId)
            .FirstOrDefaultAsync();

        var reaction = message?.Emojis.FirstOrDefault(x => x.EmojiId.ToString() == emoji);

        if (reaction is null)
        {
            return;
        }

        var user = @event.Message.Data.EventBody.DodoId;
        var island = @event.Message.Data.EventBody.IslandId;

        var openApi = provider.GetRequiredService<OpenApiService>();
        var channelLogger = provider.GetRequiredService<IChannelLogger>();

        var userRoles = (await openApi.GetMemberRoleListAsync(new GetMemberRoleListInput
        {
            DodoId = user, IslandId = island
        })).Select(x => x.RoleId).ToList();
        var islandRoles = (await openApi
                .GetRoleListAsync(new GetRoleListInput { IslandId = island }, true))
            .ToDictionary(x => x.RoleId, x => x.RoleName);
        
        var roleName = islandRoles.ContainsKey(reaction.RoleId) ? $"`{islandRoles[reaction.RoleId]}` ({reaction.RoleId})" : $"~~`未知`~~ ({reaction.RoleId})";
        
        if (isAdd)
        {
            if (userRoles.Contains(reaction.RoleId) is false)
            {
                var result = await openApi.SetRoleMemberAddAsync(new SetRoleMemberAddInput
                {
                    DodoId = user, IslandId = island, RoleId = reaction.RoleId
                });

                if (result)
                {
                    await channelLogger.LogInformation(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，已赋予身份组 {roleName}");
                }
                else
                {
                    await channelLogger.LogWarning(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，但是赋予身份组 {roleName} ***失败***");
                }
            }
            else
            {
                await channelLogger.LogInformation(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，但是其已经拥有身份组 {roleName}");
            }
        }
        else
        {
            if (userRoles.Contains(reaction.RoleId))
            {
                var result = await openApi.SetRoleMemberRemoveAsync(new SetRoleMemberRemoveInput
                {
                    DodoId = user, IslandId = island, RoleId = reaction.RoleId
                });
                
                if (result)
                {
                    await channelLogger.LogInformation(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，已撤回身份组 {roleName}");
                }
                else
                {
                    await channelLogger.LogWarning(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，但是撤回身份组 {roleName} 失败");
                }
            }
            else
            {
                await channelLogger.LogInformation(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，但是其没有身份组 {roleName}");
            }
        }
    }
}
