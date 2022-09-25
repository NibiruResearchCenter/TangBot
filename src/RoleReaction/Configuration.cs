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
using RoleReaction.Model;

namespace RoleReaction;

public sealed class Configuration : DodoHostedPluginConfiguration
{
    public override Dictionary<Type, string> RegisterMongoDbCollection()
    {
        return new Dictionary<Type, string>
        {
            { typeof(ReactionMessage), "tb-rr-messages" }
        };
    }
}
