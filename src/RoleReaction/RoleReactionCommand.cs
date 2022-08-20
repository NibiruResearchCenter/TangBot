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
using DodoHosted.Base.App.Helpers;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.App.Models;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using RoleReaction.Model;

namespace RoleReaction;

public class RoleReactionCommand : ICommandExecutor
{
    public async Task<CommandExecutionResult> Execute(string[] args, CommandMessage message, IServiceProvider provider, IPermissionManager permissionManager,
        Func<string, Task<string>> reply, bool shouldAllow = false)
    {
        var action = args.Skip(1).FirstOrDefault();
        
        if (shouldAllow is false)
        {
            var p = action switch
            {
                "list" => "rr.list",
                "creator" => "rr.creator",
                "enable" or "disable" => "rr.status",
                _ => null
            };

            if (p is null)
            {
                return CommandExecutionResult.Unknown;
            }

            if (await permissionManager.CheckPermission(p, message) is false)
            {
                return CommandExecutionResult.Unauthorized;
            }
        }

        var collection = provider.GetRequiredService<IMongoDatabase>().GetCollection<ReactionMessage>("tb-rr-messages");
        var openApi = provider.GetRequiredService<OpenApiService>();
        var islandRoles = (await openApi
            .GetRoleListAsync(new GetRoleListInput { IslandId = message.IslandId }, true))
            .ToDictionary(x => x.RoleId, x => x.RoleName);

        return args switch
        {
            [_, "list"] => await ListCommand(message.IslandId, islandRoles, collection, reply),
            [_, "enable", var id, var channel] => await StatusCommand(id, channel, openApi, islandRoles, collection, reply, true),
            [_, "disable", var id] => await StatusCommand(id, null, openApi, islandRoles, collection, reply, false),
            [_, "render", var id] => await RenderCommand(id, islandRoles, collection, reply),
            [_, "update", var id] => await UpdateCommand(id, openApi, islandRoles, collection, reply),
            [_, "creator", ..var creatorArgs] => creatorArgs switch
            {
                ["new"] => await CreatorNewCommand(collection, message.IslandId, reply),
                ["set", var rrId, var position, var content] => await CreatorSetCommand(rrId, position, content, collection, reply),
                ["role", "add", var rrId, var emoji, var role] => await CreatorRoleAddCommand(rrId, emoji, role, openApi, islandRoles, collection, reply),
                ["role", "remove",var rrId, var emoji] => await CreatorRoleRemoveCommand(rrId, emoji, openApi, islandRoles, collection, reply),
                _ => CommandExecutionResult.Unknown
            },
            _ => CommandExecutionResult.Unknown
        };
    }

    public CommandMetadata GetMetadata() => new(
        CommandName: "rr",
        Description: "配置 RoleReaction",
        HelpText: @"""
- `{{PREFIX}}rr list`  列出已有的 RR 消息
- `{{PREFIX}}rr update <RR ID>`  更新某条消息
- `{{PREFIX}}rr render <RR ID>`  渲染消息查看样式
- `{{PREFIX}}rr enable <RR ID> <频道 ID/#频道>`  启用消息并发送在某个频道中
- `{{PREFIX}}rr disable <RR ID>`  停用消息并移除在频道中的消息
- `{{PREFIX}}rr creator new`  创建一个新的消息
- `{{PREFIX}}rr creator set <RR ID> <header/footer/body> <message/template>`  设置某个部分的内容
- `{{PREFIX}}rr creator role add <RR ID> <emoji> <身份组 ID>`  设置身份组与 Emoji 的关联
- `{{PREFIX}}rr creator role remove <RR ID> <emoji>`  删除身份组与 Emoji 的关联
- `{{PREFIX}}rr creator delete <RR ID>`  删除某个 RR 消息
""",
        PermissionNodes: new Dictionary<string, string>
        {
            { "rr.list", "查看 RR 消息信息" },
            { "rr.creator", "创建或编辑 RR 消息" },
            { "rr.status", "开启或关闭 RR 消息" }
        });

    // ReSharper disable InconsistentNaming
    private const string MESSAGE_SEND_FAILED = "MESSAGE_SEND_FAILED";
    private const string MESSAGE_UPDATE_FAILED = "MESSAGE_UPDATE_FAILED";
    
    private static async Task<CommandExecutionResult> ListCommand(
        string islandId,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var result = collection
            .AsQueryable()
            .Where(x => x.IslandId == islandId)
            .ToList();
        
        var builder = new StringBuilder();

        builder.AppendLine($"共有 ***{result.Count}*** 条 RR 消息");
        foreach (var message in result)
        {
            var channel = message.Enabled ? $"<#{message.Channel}>" : "未开启";
            builder.AppendLine($"- `{message.Id}` 位于频道 {channel}");
            foreach (var reactionEmoji in message.Emojis)
            {
                var roleName = islandRoles.ContainsKey(reactionEmoji.RoleId) ? $"`{islandRoles[reactionEmoji.RoleId]}`" : "~~`未知`~~";
                builder.AppendLine($"  $ {reactionEmoji.EmojiCode} ({reactionEmoji.EmojiId}, `U+{reactionEmoji.EmojiId:X4}`): {roleName} ({reactionEmoji.RoleId})");
            }
        }

        await reply.Invoke(builder.ToString());

        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> StatusCommand(
        string id,
        string? channel,
        OpenApiService openApiService,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply,
        bool isEnable)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        if (isEnable is false)
        {
            message.Enabled = false;
            if (string.IsNullOrEmpty(message.MessageId) is false)
            {
                var result = await openApiService.SetChannelMessageWithdrawAsync(new SetChannelMessageWithdrawInput
                {
                    MessageId = message.MessageId, Reason = "RR 消息已停用"
                });

                if (result is false)
                {
                    await reply.Invoke("RR 消息撤回失败，请手动撤回");
                }
            }

            message.Channel = string.Empty;
            message.MessageId = string.Empty;
            await collection.ReplaceOneAsync(x => x.Id == rrId, message);
            await reply.Invoke("已 ***关闭*** 该消息");

            return CommandExecutionResult.Success;
        }

        var channelId = channel?.ExtractChannelId();
        if (channelId is null)
        {
            await reply.Invoke("频道 ID 不能为空");
            return CommandExecutionResult.Failed;
        }
        
        message.Enabled = true;
        message.Channel = channelId;
        var msgId = await UpdateMessage(message, islandRoles, openApiService);
        
        if (msgId is MESSAGE_SEND_FAILED or MESSAGE_UPDATE_FAILED)
        {
            await reply.Invoke("RR 消息发送或更新失败");
            return CommandExecutionResult.Failed;
        }

        message.MessageId = msgId;
        
        await collection.ReplaceOneAsync(x => x.Id == rrId, message);
        await reply.Invoke("已 ***开启*** 该消息");
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> CreatorNewCommand(
        IMongoCollection<ReactionMessage> collection,
        string islandId,
        Func<string, Task<string>> reply)
    {
        var newMessage = new ReactionMessage
        {
            IslandId = islandId,
            Channel = string.Empty,
            Emojis = new List<ReactionEmoji>(),
            Enabled = false,
            HeaderText = "点击 Emoji 获取身份组",
            FooterText = string.Empty,
            BodyTemplate = "- 点击 {{Emoji}} 获取 {{Role}} 身份组"
        };

        await collection.InsertOneAsync(newMessage);

        var id = newMessage.Id;
        await reply.Invoke($"已创建新的消息，ID：{id}");
        
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> CreatorSetCommand(
        string id,
        string position,
        string content,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        string replyMessage;
        switch (position)
        {
            case "header":
                message.HeaderText = content;
                replyMessage = $"已修改头部文本为 `{content}`";
                break;
            case "footer":
                message.FooterText = content;
                replyMessage = $"已修改底部文本为 `{content}`";
                break;
            case "body":
                message.BodyTemplate = content;
                replyMessage = $"已修改本体模版为 `{content}`";
                break;
            default:
                return CommandExecutionResult.Unknown;
        }

        await collection.FindOneAndReplaceAsync(x => x.Id == rrId, message);
        await reply.Invoke(replyMessage);

        return CommandExecutionResult.Success;
    }
    
    private static async Task<CommandExecutionResult> CreatorRoleAddCommand(
        string id,
        string emoji,
        string roleId,
        OpenApiService openApiService,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }

        var emojiId = emoji.GetEmojiId();
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        if (islandRoles.ContainsKey(roleId) is false)
        {
            await reply.Invoke($"身份组 {roleId} 不存在");
            return CommandExecutionResult.Failed;
        }

        var emojiExists = message.Emojis.FirstOrDefault(x => x.EmojiId == emojiId);
        if (emojiExists is not null)
        {
            await reply.Invoke($"Emoji {emojiExists.EmojiCode} ({emojiExists.EmojiId}) 已被使用，权限组 `{islandRoles[emojiExists.RoleId]}` ({emojiExists.RoleId})");
            return CommandExecutionResult.Failed;
        }

        message.Emojis.Add(new ReactionEmoji
        {
            EmojiId = emojiId,
            EmojiCode = emoji,
            RoleId = roleId
        });

        if (message.Enabled)
        {
            var msgId = await UpdateMessage(message, islandRoles, openApiService);

            if (msgId is MESSAGE_SEND_FAILED or MESSAGE_UPDATE_FAILED)
            {
                await reply.Invoke("更新或发送消息失败");
            }
        }
        
        await collection.FindOneAndReplaceAsync(x => x.Id == rrId, message);
        await reply.Invoke($"已添加 Reaction: {emoji} ({emojiId}) -> `{islandRoles[roleId]}` ({roleId})");
        return CommandExecutionResult.Success;
    }
    
    private static async Task<CommandExecutionResult> CreatorRoleRemoveCommand(
        string id,
        string emoji,
        OpenApiService openApiService,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        var emojiId = emoji.GetEmojiId();
        
        var emojiExists = message.Emojis.FirstOrDefault(x => x.EmojiId == emojiId);
        if (emojiExists is null)
        {
            await reply.Invoke($"Emoji {emoji} 不存在");
            return CommandExecutionResult.Failed;
        }

        var roleId = emojiExists.RoleId;
        message.Emojis.Remove(emojiExists);

        if (message.Enabled)
        {
            var result = await openApiService.SetChannelMessageReactionRemoveAsync(new SetChannelMessageReactionRemoveInput
            {
                DodoId = string.Empty,
                MessageId = message.MessageId,
                Emoji = new MessageModelEmoji { Id = emojiId.ToString(), Type = 1 }
            });

            if (result is false)
            {
                await reply.Invoke("移除已添加的 Reaction 失败");
            }
        }

        await collection.FindOneAndReplaceAsync(x => x.Id == rrId, message);
        var roleName = islandRoles.ContainsKey(roleId) ? $"`{islandRoles[roleId]}`" : "~~`未知`~~";
        await reply.Invoke($"已移除 Reaction: {emoji} ({emojiId}) -> {roleName} ({roleId})");
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> RenderCommand(
        string id,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        var renderMsg = RenderTextMessage(message, islandRoles);
        await reply.Invoke(renderMsg);
        return CommandExecutionResult.Success;
    }

    private static async Task<CommandExecutionResult> UpdateCommand(
        string id,
        OpenApiService openApiService,
        IReadOnlyDictionary<string, string> islandRoles,
        IMongoCollection<ReactionMessage> collection,
        Func<string, Task<string>> reply)
    {
        var isGuid = Guid.TryParse(id, out var rrId);
        if (isGuid is false)
        {
            await reply.Invoke("ID 错误");
            return CommandExecutionResult.Failed;
        }
        
        var message = await collection.Find(x => x.Id == rrId).FirstOrDefaultAsync();
        if (message is null)
        {
            await reply.Invoke("没有找到该消息");
            return CommandExecutionResult.Failed;
        }

        if (message.Enabled is false)
        {
            await reply.Invoke("消息未启用");
            return CommandExecutionResult.Failed;
        }

        var msgId = await UpdateMessage(message, islandRoles, openApiService);
        
        if (msgId is MESSAGE_SEND_FAILED or MESSAGE_UPDATE_FAILED)
        {
            await reply.Invoke("RR 消息发送或更新失败");
            return CommandExecutionResult.Failed;
        }
        
        await reply.Invoke("消息已更新");
        return CommandExecutionResult.Success;
    }

    private static string RenderTextMessage(ReactionMessage message, IReadOnlyDictionary<string, string> islandRoles)
    {
        var builder = new StringBuilder();

        if (string.IsNullOrWhiteSpace(message.HeaderText) is false)
        {
            builder.AppendLine(message.HeaderText);
            builder.AppendLine();
        }

        var bodyText = from emoji in message.Emojis
            let roleName = islandRoles.ContainsKey(emoji.RoleId) ? $"`{islandRoles[emoji.RoleId]}`" : "~~`未知`~~"
            select message.BodyTemplate
                .Replace("{{Emoji}}", emoji.EmojiCode)
                .Replace(" {{Role}}", roleName);

        builder.AppendJoin('\n', bodyText);

        if (string.IsNullOrWhiteSpace(message.FooterText) is false)
        {
            builder.AppendLine();
            builder.AppendLine(message.FooterText);
        }

        var msg = builder.ToString();
        return msg;
    }

    private static async Task<string> UpdateMessage(
        ReactionMessage message,
        IReadOnlyDictionary<string, string> islandRoles,
        OpenApiService openApiService)
    {
        var renderMsg = RenderTextMessage(message, islandRoles);
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
}
