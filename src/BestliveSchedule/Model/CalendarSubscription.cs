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

namespace BestliveSchedule.Model;

public record CalendarSubscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public required string Name { get; set; }
    public required string CalDavUrl { get; set; }
    public required string BasicAuthString { get; set; }
    public required int TimezoneOffset { get; set; }
}
