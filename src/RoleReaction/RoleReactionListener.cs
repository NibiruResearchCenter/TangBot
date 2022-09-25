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
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.Events;
using DodoHosted.Open.Plugin;
using MongoDB.Driver;
using RoleReaction.Model;

namespace RoleReaction;

public sealed class RoleReactionListener : IEventHandler<DodoMessageReactionEvent>
{
    private readonly IMongoCollection<ReactionMessage> _collection;
    private readonly IChannelLogger _channelLogger;
    private readonly OpenApiService _openApiService;

    public RoleReactionListener(
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] IChannelLogger channelLogger,
        [Inject] OpenApiService openApiService)
    {
        _collection = collection;
        _channelLogger = channelLogger;
        _openApiService = openApiService;
    }
    
    public async Task Handle(DodoMessageReactionEvent eventContext)
    {
        var msgId = eventContext.Message.Data.EventBody.ReactionTarget.Id;
        var emoji = eventContext.Message.Data.EventBody.ReactionEmoji.Id;
        var isAdd = eventContext.Message.Data.EventBody.ReactionType == 1;
        
        var message = await _collection
            .Find(x => x.MessageId == msgId)
            .FirstOrDefaultAsync();

        var reaction = message?.Emojis.FirstOrDefault(x => x.EmojiId.ToString() == emoji);

        if (reaction is null)
        {
            return;
        }

        var user = eventContext.Message.Data.EventBody.DodoId;
        var island = eventContext.Message.Data.EventBody.IslandId;

        var userRoles = (await _openApiService.GetMemberRoleListAsync(new GetMemberRoleListInput
        {
            DodoId = user, IslandId = island
        })).Select(x => x.RoleId).ToList();
        var islandRoles = (await _openApiService
                .GetRoleListAsync(new GetRoleListInput { IslandId = island }, true))
            .ToDictionary(x => x.RoleId, x => x.RoleName);
        
        var roleName = islandRoles.ContainsKey(reaction.RoleId) ? $"`{islandRoles[reaction.RoleId]}` ({reaction.RoleId})" : $"~~`未知`~~ ({reaction.RoleId})";
        
        if (isAdd)
        {
            if (userRoles.Contains(reaction.RoleId) is false)
            {
                var result = await _openApiService.SetRoleMemberAddAsync(new SetRoleMemberAddInput
                {
                    DodoId = user, IslandId = island, RoleId = reaction.RoleId
                });

                if (result)
                {
                    await _channelLogger.LogInformation(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，已赋予身份组 {roleName}");
                }
                else
                {
                    await _channelLogger.LogWarning(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，但是赋予身份组 {roleName} ***失败***");
                }
            }
            else
            {
                await _channelLogger.LogInformation(island, $"用户 <@!{user}> 选择 {reaction.EmojiCode}，但是其已经拥有身份组 {roleName}");
            }
        }
        else
        {
            if (userRoles.Contains(reaction.RoleId))
            {
                var result = await _openApiService.SetRoleMemberRemoveAsync(new SetRoleMemberRemoveInput
                {
                    DodoId = user, IslandId = island, RoleId = reaction.RoleId
                });
                
                if (result)
                {
                    await _channelLogger.LogInformation(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，已撤回身份组 {roleName}");
                }
                else
                {
                    await _channelLogger.LogWarning(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，但是撤回身份组 {roleName} 失败");
                }
            }
            else
            {
                await _channelLogger.LogInformation(island, $"用户 <@!{user}> 取消选择 {reaction.EmojiCode}，但是其没有身份组 {roleName}");
            }
        }
    }
}
