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

using DodoHosted.Open.Plugin;
using Microsoft.Extensions.Logging;

namespace RoleReaction;

public class Entry : IPluginLifetime
{
    public Task Load(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("已载入 RoleReaction 插件");
        
        return Task.CompletedTask;
    }

    public Task Unload(ILogger logger)
    {
        logger.LogInformation("已卸载 RoleReaction 插件");
        
        return Task.CompletedTask;
    }
}
