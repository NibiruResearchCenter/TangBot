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
using DoDo.Open.Sdk.Models.Channels;
using DoDo.Open.Sdk.Models.Messages;
using DoDo.Open.Sdk.Models.Roles;
using DoDo.Open.Sdk.Services;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Base.App.Command;
using DodoHosted.Base.App.Context;
using DodoHosted.Base.App.Types;
using DodoHosted.Base.Card.Enums;
using DodoHosted.Open.Plugin;
using MongoDB.Driver;
using RoleReaction.Model;

// ReSharper disable MemberCanBeMadeStatic.Global
// ReSharper disable MemberCanBePrivate.Global

namespace RoleReaction;

public sealed class RoleReactionCommand : ICommandExecutor
{
    // ReSharper disable InconsistentNaming
    private const string MESSAGE_SEND_FAILED = "MESSAGE_SEND_FAILED";
    private const string MESSAGE_UPDATE_FAILED = "MESSAGE_UPDATE_FAILED";
    
    public async Task<bool> ListReactionMessages(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("page", "p", "页码", false)] int? page)
    {
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId)
            .ToListAsync();

        const int PageSize = 5;
        
        var p = page ?? 1;
        var total = result.Count;
        var pages = (int)Math.Ceiling(total / (double) PageSize);
        
        var messages = result.Skip(PageSize * (p - 1)).Take(PageSize).ToArray();
        if (messages.Length == 0)
        {
            if (p == 1)
            {
                await context.Reply.Invoke("没有找到 RR 消息");
            }
            else
            {
                await context.Reply.Invoke("没有更多的 RR 消息了");
            }

            return true;
        }

        var channels = await openApiService.GetChannelListAsync(new GetChannelListInput
        {
            IslandId = context.EventInfo.IslandId
        });

        var card = RoleReactionCardMessages
            .GetRoleReactionMessageListCard($"RR 消息列表 ({p}/{pages})", CardTheme.Indigo, channels, messages);

        await context.ReplyCard.Invoke(card);
        return true;
    }

    public async Task<bool> GetReactionMessageDetail(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }
        
        var channels = await openApiService.GetChannelListAsync(new GetChannelListInput
        {
            IslandId = context.EventInfo.IslandId
        });
        var roles = await openApiService.GetRoleListAsync(new GetRoleListInput
        {        
            IslandId = context.EventInfo.IslandId
        });
        
        var card = result.GetRoleReactionMessageDetailCard("RR 消息详情", CardTheme.Indigo, channels, roles);
        await context.ReplyCard.Invoke(card);
        return true;
    }

    public async Task<bool> CreateNewMessage(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection)
    {
        var newMessage = new ReactionMessage
        {
            IslandId = context.EventInfo.IslandId,
            Channel = string.Empty,
            Emojis = new List<ReactionEmoji>(),
            Enabled = false,
            HeaderText = "点击 Emoji 获取身份组",
            FooterText = string.Empty,
            BodyTemplate = "- 点击 {{Emoji}} 获取 {{Role}} 身份组"
        };
        
        await collection.InsertOneAsync(newMessage);
        
        var id = newMessage.Id;
        await context.Reply.Invoke($"已创建新的消息，ID：{id}");

        return true;
    }

    public async Task<bool> SetMessageTemplate(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("position", "p", "位置，可以为 `header` `body` `footer`，在 `body` 中使用 `{{Emoji}}` 标记 Emoji 位置，`{{Role}}` 标记身份组名")] string position,
        [CmdOption("template", "t", "模版")] string template,
        [CmdOption("id", "i", "RR 消息 ID")] string id)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }
        
        switch (position)
        {
            case "header":
                result.HeaderText = template;
                break;
            case "body":
                result.BodyTemplate = template;
                break;
            case "footer":
                result.FooterText = template;
                break;
            default:
                await context.Reply.Invoke("无效的位置");
                return false;
        }
        
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        var channels = await openApiService.GetChannelListAsync(new GetChannelListInput
        {
            IslandId = context.EventInfo.IslandId
        });
        var roles = await openApiService.GetRoleListAsync(new GetRoleListInput
        {
            IslandId = context.EventInfo.IslandId
        });
        
        var card = result.GetRoleReactionMessageDetailCard("RR 消息详情", CardTheme.Indigo, channels, roles);
        await context.ReplyCard.Invoke(card);
        return true;
    }

    public async Task<bool> AddRole(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id,
        [CmdOption("role", "r", "身份组 ID")] string roleId,
        [CmdOption("emoji", "e", "Emoji")] DodoEmoji emoji)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }

        if (result.Emojis.Any(x => x.EmojiId == emoji.EmojiId) || result.Emojis.Any(x => x.RoleId == roleId))
        {
            await context.Reply.Invoke("已存在相同的 Emoji 或身份组");
            return false;
        }

        var roleList = await openApiService.GetRoleListAsync(new GetRoleListInput
        {
            IslandId = context.EventInfo.IslandId
        }, true);

        var role = roleList.FirstOrDefault(x => x.RoleId == roleId);
        if (role is null)
        {
            await context.Reply.Invoke($"没有找到 ID 为 {roleId} 的身份组");
            return false;
        }
        
        result.Emojis.Add(new ReactionEmoji
        {
            EmojiCode = emoji.Emoji,
            EmojiId = emoji.EmojiId,
            RoleId = roleId
        });
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        await context.Reply.Invoke($"已添加 {emoji.Emoji} - {role.RoleName} ({role.RoleId}");
        return true;
    }

    public async Task<bool> RemoveRole(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [CmdOption("id", "i", "RR 消息 ID")] string id,
        [CmdOption("emoji", "e", "Emoji")] DodoEmoji emoji)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }

        if (result.Emojis.Any(x => x.EmojiId == emoji.EmojiId) is false)
        {
            await context.Reply.Invoke("没有找到相应的 Emoji");
            return false;
        }

        result.Emojis.RemoveAll(x => x.EmojiId == emoji.EmojiId);
        
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        await context.Reply.Invoke($"已移除 {emoji.Emoji}");
        return true;
    }

    public async Task<bool> RenderPreviewMessage(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }

        var roles = await openApiService.GetRoleListAsync(new GetRoleListInput
        {
            IslandId = context.EventInfo.IslandId
        }, true);

        var message = RenderTextMessage(result, roles);
        await context.Reply.Invoke(message);
        return true;
    }

    public async Task<bool> UpdateMessage(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }

        if (result.Enabled is false)
        {
            await context.Reply.Invoke("该 RR 消息未开启");
            return false;
        }
        
        var roles = await openApiService.GetRoleListAsync(new GetRoleListInput
        {
            IslandId = context.EventInfo.IslandId
        }, true);

        var response = await UpdateTextMessage(roles, result, openApiService);
        if (response is MESSAGE_SEND_FAILED or MESSAGE_UPDATE_FAILED)
        {
            await context.Reply.Invoke($"发送或更新消息失败 {response}");
            return false;
        }

        result.MessageId = response;
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        
        await context.Reply.Invoke("已更新消息");
        return true;
    }

    public async Task<bool> EnableMessage(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id,
        [CmdOption("channel", "c", "发送频道")] DodoChannelId channel)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }
        
        if (result.Enabled)
        {
            await context.Reply.Invoke("该 RR 消息已开启");
            return false;
        }
        
        var roles = await openApiService.GetRoleListAsync(new GetRoleListInput
        {
            IslandId = context.EventInfo.IslandId
        }, true);

        result.Channel = channel.Value;
        var response = await UpdateTextMessage(roles, result, openApiService);
        if (response is MESSAGE_SEND_FAILED or MESSAGE_UPDATE_FAILED)
        {
            await context.Reply.Invoke($"发送或更新消息失败 {response}");
            return false;
        }
        
        result.MessageId = response;
        result.Enabled = true;
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        await context.Reply.Invoke("已开启 RR 消息");
        return true;
    }

    public async Task<bool> DisableMessage(
        CommandContext context,
        [Inject] IMongoCollection<ReactionMessage> collection,
        [Inject] OpenApiService openApiService,
        [CmdOption("id", "i", "RR 消息 ID")] string id)
    {
        var parsed = Guid.TryParse(id, out var guid);
        if (parsed is false)
        {
            await context.Reply.Invoke("无效的 RR 消息 ID");
            return false;
        }
        
        var result = await collection
            .Find(x => x.IslandId == context.EventInfo.IslandId && x.Id == guid)
            .FirstOrDefaultAsync();
        if (result is null)
        {
            await context.Reply.Invoke("没有找到 RR 消息");
            return false;
        }
        
        if (result.Enabled is false)
        {
            await context.Reply.Invoke("该 RR 消息已关闭");
            return false;
        }

        await openApiService.SetChannelMessageWithdrawAsync(new SetChannelMessageWithdrawInput
        {
            MessageId = result.MessageId, Reason = "RR 消息已关闭"
        }, true);
        
        result.Enabled = false;
        await collection.FindOneAndReplaceAsync(x => x.Id == guid, result);
        await context.Reply.Invoke("已关闭 RR 消息");
        return true;
    }
    
    public CommandTreeBuilder GetBuilder()
    {
        return new CommandTreeBuilder("rr", "Role Reaction 消息", "rr")
            .Then("list", "列出所有 RR 消息", "info", ListReactionMessages)
            .Then("info", "查看 RR 消息信息", "info", GetReactionMessageDetail)
            .Then("creator", "创建 RR 消息", "creator", builder: x => x
                .Then("new", "创建新的 RR 消息", string.Empty, CreateNewMessage)
                .Then("set", "设置消息组件", string.Empty, SetMessageTemplate)
                .Then("add", "添加新的反应", string.Empty, AddRole)
                .Then("remove", "移除一个反应", string.Empty, RemoveRole))
            .Then("enable", "开启 RR 消息", "creator", EnableMessage)
            .Then("disable", "关闭 RR 消息", "creator", DisableMessage)
            .Then("render", "渲染预览消息", "creator", RenderPreviewMessage)
            .Then("update", "更新 RR 消息", "creator", UpdateMessage);
    }

    private static async Task<string> UpdateTextMessage(IEnumerable<GetRoleListOutput> roleList, ReactionMessage message, OpenApiService openApiService)
    {
        var renderMsg = RenderTextMessage(message, roleList);
        var emojiList = message.Emojis.Select(x => x.EmojiCode).ToList();
        
        if (string.IsNullOrEmpty(message.MessageId))
        {
            var sendResult = await openApiService.SetChannelMessageSendAsync(new SetChannelMessageSendInput<MessageBodyText>
            {
                ChannelId = message.Channel, MessageBody = new MessageBodyText { Content = renderMsg }
            });
        
            if (sendResult is null)
            {
                return MESSAGE_SEND_FAILED;
            }
            
            message.MessageId = sendResult.MessageId;
        }
        else
        {
            var updateResult = await openApiService.SetChannelMessageEditAsync(
                new SetChannelMessageEditInput<MessageBodyText>
                {
                    MessageBody = new MessageBodyText { Content = renderMsg }, MessageId = message.MessageId
                });
        
            if (updateResult is false)
            {
                return MESSAGE_UPDATE_FAILED;
            }
        }
        
        foreach (var emoji in emojiList)
        {
            await openApiService.SetChannelMessageReactionAddAsync(new SetChannelMessageReactionAddInput
            {
                Emoji = new MessageModelEmoji { Id = emoji.EnumerateRunes().First().Value.ToString(), Type = 1 },
                MessageId = message.MessageId
            });
        }
        
        return message.MessageId;
    }
    
    private static string RenderTextMessage(ReactionMessage message, IEnumerable<GetRoleListOutput> roleList)
    {
        var builder = new StringBuilder();
    
        if (string.IsNullOrWhiteSpace(message.HeaderText) is false)
        {
            builder.AppendLine(message.HeaderText);
            builder.AppendLine();
        }
    
        var bodyText = from emoji in message.Emojis
            let roleName = roleList.FirstOrDefault(x => x.RoleId == emoji.RoleId)?.RoleName ?? "~~`未知`~~"
            select message.BodyTemplate
                .Replace("{{Emoji}}", emoji.EmojiCode)
                .Replace("{{Role}}", roleName);
    
        builder.AppendJoin('\n', bodyText);
    
        if (string.IsNullOrWhiteSpace(message.FooterText) is false)
        {
            builder.AppendLine();
            builder.AppendLine(message.FooterText);
        }
    
        var msg = builder.ToString();
        return msg;
    }
}
