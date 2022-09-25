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

using DoDo.Open.Sdk.Models.Channels;
using DoDo.Open.Sdk.Models.Roles;
using DodoHosted.Base.Card;
using DodoHosted.Base.Card.BaseComponent;
using DodoHosted.Base.Card.CardComponent;
using DodoHosted.Base.Card.Enums;
using RoleReaction.Model;

namespace RoleReaction;

public static class RoleReactionCardMessages
{
    public static CardMessage GetRoleReactionMessageListCard(
        string title,
        CardTheme theme,
        IEnumerable<GetChannelListOutput> channelList,
        params ReactionMessage[] reactionMessages)
    {
        var cardComponents = reactionMessages
            .Select(x => x.GetRoleReactionMessageCardComponents(channelList))
            .Aggregate((x, y) => x.Append(new Divider()).Concat(y));

        return new CardMessage(new Card { Title = title, Theme = theme, Components = cardComponents.ToList() });
    }
    
    public static CardMessage GetRoleReactionMessageDetailCard(
        this ReactionMessage reactionMessage,
        string title,
        CardTheme theme,
        IEnumerable<GetChannelListOutput> channelList,
        IEnumerable<GetRoleListOutput> roleList)
    {
        var cardComponents = reactionMessage.GetRoleReactionMessageCardDetailComponents(
            channelList, roleList.ToArray());
        
        return new CardMessage(new Card { Title = title, Theme = theme, Components = cardComponents.ToList() });
    }

    private static IEnumerable<ICardComponent> GetRoleReactionMessageCardComponents(
        this ReactionMessage reactionMessage,
        IEnumerable<GetChannelListOutput> channelList)
    {
        var channelId = string.IsNullOrEmpty(reactionMessage.Channel) ? "未设置" : reactionMessage.Channel;
        var channel = channelList.FirstOrDefault(x => x.ChannelId == reactionMessage.Channel);
        var channelName = channel is null ? "未知" : channel.ChannelName;
        var messageId = string.IsNullOrEmpty(reactionMessage.MessageId) ? "未发送" : reactionMessage.MessageId;

        return new List<ICardComponent>
        {
            new MultilineText(new Text("ID"), new Text(reactionMessage.Id.ToString())),
            new MultilineText(new Text("频道 ID"), new Text(channelId)),
            new MultilineText(new Text("频道名"), new Text(channelName)),
            new MultilineText(new Text("消息 ID"), new Text(messageId)),
            new MultilineText(new Text("开启状态"), new Text(reactionMessage.Enabled ? "✅" : "❌")),
            new MultilineText(new Text("Emoji 数量"), new Text(reactionMessage.Emojis.Count.ToString()))
        };
    }

    private static IEnumerable<ICardComponent> GetRoleReactionMessageCardDetailComponents(
        this ReactionMessage reactionMessage,
        IEnumerable<GetChannelListOutput> channelList,
        GetRoleListOutput[] roleList)
    {
        var components = reactionMessage
            .GetRoleReactionMessageCardComponents(channelList)
            .ToList();

        components.AddRange(new ICardComponent[]
        {
            new MultilineText(new Text("Header"), new Text(string.IsNullOrEmpty(reactionMessage.HeaderText) ? "<未设置>" : reactionMessage.HeaderText)),
            new MultilineText(new Text("Body 模版"), new Text(string.IsNullOrEmpty(reactionMessage.BodyTemplate) ? "<未设置>" : reactionMessage.BodyTemplate)),
            new MultilineText(new Text("Footer"), new Text(string.IsNullOrEmpty(reactionMessage.FooterText) ? "<未设置>" : reactionMessage.FooterText)),
            new Divider(),
        });

        if (reactionMessage.Emojis.Count == 0)
        {
            components.Add(new TextFiled("无 `Emoji - 身份组` 对"));
        }
        else
        {
            components.AddRange(
                from reactionEmoji in reactionMessage.Emojis
                let roleName = roleList
                    .FirstOrDefault(x => x.RoleId == reactionEmoji.RoleId)?
                    .RoleName ?? "<未知的身份组>"
                select new MultilineText(
                    new Text($"{roleName} ({reactionEmoji.RoleId})"),
                    new Text(reactionEmoji.EmojiCode)));
        }
        
        return components;
    }
}
