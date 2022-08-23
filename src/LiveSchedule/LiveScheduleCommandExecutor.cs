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

using DodoHosted.Base;
using DodoHosted.Base.App.Interfaces;
using DodoHosted.Base.App.Models;
using DodoHosted.Open.Plugin;

namespace LiveSchedule;

public class LiveScheduleCommandExecutor : ICommandExecutor
{
    public async Task<CommandExecutionResult> Execute(
        string[] args,
        CommandMessage message,
        IServiceProvider provider,
        IPermissionManager permissionManager,
        PluginBase.Reply reply,
        bool shouldAllow = false)
    {
        throw new NotImplementedException();
    }

    public CommandMetadata GetMetadata() => new(
        CommandName: "live-schedule",
        Description: "直播时间表插件",
        HelpText: @"""
- `{{PREFIX}}live-schedule liver list`  查看主播列表
- `{{PREFIX}}live-schedule liver add <@用户/用户 ID> <Bilibili UID>`  添加一位主播
- `{{PREFIX}}live-schedule liver remove <@用户/用户 ID>`  删除一位主播
""",
        PermissionNodes: new Dictionary<string, string>
        {
            { "live-schedule.liver", "主播权限组" }
        });
}
