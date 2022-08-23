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

using DodoHosted.Base.Events;
using DodoHosted.Open.Plugin;
using Microsoft.Extensions.Logging;

namespace LiveSchedule;

public class CardFormReportListener : IDodoHostedPluginEventHandler<DodoCardMessageFormSubmitEvent>
{
    public Task Handle(DodoCardMessageFormSubmitEvent @event, IServiceProvider provider, ILogger logger)
    {
        throw new NotImplementedException();
    }

    private Task HandleAddNewWeeklyLiveSchedule(DodoCardMessageFormSubmitEvent @event,
        IServiceProvider serviceProvider, ILogger logger)
    {
        throw new NotImplementedException();
    }
    
    private Task HandleDeleteLiveSchedule(DodoCardMessageFormSubmitEvent @event,
        IServiceProvider serviceProvider, ILogger logger)
    {
        throw new NotImplementedException();
    }
}
