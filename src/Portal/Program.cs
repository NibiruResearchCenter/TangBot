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

using DodoHosted.App.Core;
using DodoHosted.Lib.Plugin.Services;
using Serilog;
using Serilog.Events;

PluginLoadingManager.NativeAssemblies.AddRange(new []
{
    typeof(RoleReaction.Configuration).Assembly
    // typeof(LiveSchedule.Entry).Assembly
    // typeof(BestLiveSchedule.Entry).Assembly
});

const string LoggerTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] <{ThreadId} {ThreadName}> {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: LoggerTemplate)
    .WriteTo.File(outputTemplate: LoggerTemplate, path: "logs/log.txt", rollingInterval: RollingInterval.Day)
    .Enrich.WithThreadId()
    .Enrich.WithThreadName()
    .Enrich.FromLogContext()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .CreateLogger();

var builder = WebApplication.CreateBuilder();

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

builder.Services.AddDodoHostedServices();
builder.Services.AddDodoHostedWebServices();

var app = builder.Build();

app.UseDodoHostedWebPipeline();

await app.RunAsync();
