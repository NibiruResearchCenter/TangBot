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
using DodoHosted.Base.App;
using DodoHosted.Base.App.Attributes;
using DodoHosted.Open.Plugin;

namespace BilibiliLiveNotification;

public sealed class Lifetime : DodoHostedPluginLifetime
{
    public Lifetime([Inject] PluginConfigurationManager pluginConfigurationManager)
    {
        try
        {
            pluginConfigurationManager.GetObjectValue<PluginConfiguration>("config")
                .GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            pluginConfigurationManager.SetObjectValue("config", new PluginConfiguration { JobRunningInterval = 120 })
                .GetAwaiter().GetResult();
        }
    }
    
    public override Task OnLoad()
    {
        return Task.CompletedTask;
    }

    public override Task OnDestroy()
    {
        return Task.CompletedTask;
    }
}
