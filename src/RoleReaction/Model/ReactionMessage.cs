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

using MongoDB.Bson.Serialization.Attributes;

namespace RoleReaction.Model;

public record ReactionMessage
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public required string IslandId { get; init; }
    
    public required string HeaderText { get; set; }
    public required string FooterText { get; set; }
    public required string BodyTemplate { get; set; }
    
    public required List<ReactionEmoji> Emojis { get; set; }
    
    public required string Channel { get; set; }
    public required bool Enabled { get; set; }
    public string MessageId { get; set; } = string.Empty;
}
