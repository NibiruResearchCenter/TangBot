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
using DodoHosted.Lib.Plugin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

PluginManager.NativeAssemblies.AddRange(new []
{
    typeof(RoleReaction.Entry).Assembly
});

const string LoggerTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] <{ThreadId} {ThreadName}> {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: LoggerTemplate)
    .Enrich.WithThreadId()
    .Enrich.WithThreadName()
    .Enrich.FromLogContext()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .CreateLogger();

var builder = Host.CreateDefaultBuilder();

builder.ConfigureLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
});

builder.UseSerilog();

builder.ConfigureServices((_, services) =>
{
    services.AddDodoHostedServices();
});

var app = builder.Build();

await app.RunAsync();
